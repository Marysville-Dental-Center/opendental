// CDT: open source replacement for the prebuilt CDT.dll.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Text;

namespace CDT {
	///<summary>Keyed hashes for the SecurityHash tamper-detection columns (appointment, claim, claimproc, patient, payplan, paysplit).
	///Format: "osh1$" + base64(HMAC-SHA256(key, UTF-8 text)), with separate keys for the patient table and for every other table.
	///The key is fixed and derived from a public constant, so this only catches edits made directly in the database by someone who doesn't
	///recompute the hash. See CDT/README.md.</summary>
	internal sealed class SaltedHash : IDisposable {
		///<summary>Version prefix. Hashes without it were written by Open Dental before this project took over the database.</summary>
		internal const string Prefix="osh1$";
		///<summary>Public key material for osh1. Changing it invalidates every stored osh1 hash until SecurityHash.UpdateHashing runs again.</summary>
		internal const string KeyMaterial="CDT osh1 SecurityHash key. Public by design; see CDT/README.md.";
		private readonly HmacSha256 _hmacRow;
		private readonly HmacSha256 _hmacPatient;

		internal SaltedHash() {
			byte[] arrayMaster=HmacSha256.Sha256(Encoding.UTF8.GetBytes(KeyMaterial));
			_hmacRow=new HmacSha256(HmacSha256.Compute(arrayMaster,Encoding.ASCII.GetBytes("osh1 row")));
			_hmacPatient=new HmacSha256(HmacSha256.Compute(arrayMaster,Encoding.ASCII.GetBytes("osh1 patient")));
		}

		internal string Compute(string unhashedText,bool isPatientTable) {
			byte[] arrayText=Encoding.UTF8.GetBytes(unhashedText??"");
			HmacSha256 hmac=isPatientTable ? _hmacPatient : _hmacRow;
			return Prefix+Convert.ToBase64String(hmac.ComputeHash(arrayText));
		}

		internal static bool IsCurrentFormat(string hash) {
			return hash!=null && hash.StartsWith(Prefix,StringComparison.Ordinal);
		}

		public void Dispose() {
			_hmacRow.Dispose();
			_hmacPatient.Dispose();
		}
	}
}
