// Vendor-direct integrations: reading and writing an integration's settings and secrets in programproperty rows.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness.VendorDirect {
	///<summary>Settings and secrets for one integration, kept in its programproperty rows. Missing rows are inserted at runtime
	///(ProgramProperties.InsertIfMissing), so there is no schema change and no ConvertDatabases step. Secret rows are always IsMasked=1 and
	///IsHighSecurity=1, so editing them needs ManageHighSecurityProgProperties. All of this is safe under the middle tier: the database work goes
	///through ProgramProperties methods that forward to the server; encryption happens on the client.</summary>
	public static class VendorCredentials {
		///<summary>Inserts a plain setting row if missing. Returns the row.</summary>
		public static ProgramProperty EnsureSetting(long programNum,string desc,long clinicNum=0,string defaultValue="") {
			Meth.NoCheckMiddleTierRole();
			return ProgramProperties.InsertIfMissing(programNum,desc,clinicNum,defaultValue,false,false);
		}

		///<summary>Inserts a secret row (blank, masked, high security) if missing, and raises the flags on an existing row. Returns the row.</summary>
		public static ProgramProperty EnsureSecret(long programNum,string desc,long clinicNum=0) {
			Meth.NoCheckMiddleTierRole();
			return ProgramProperties.InsertIfMissing(programNum,desc,clinicNum,"",true,true);
		}

		///<summary>Reads a secret from the cache: the clinic row, else the ClinicNum 0 row. Never throws.</summary>
		public static SecretReadResult ReadSecret(long programNum,string desc,long clinicNum,LegacyPolicy legacyPolicy) {
			Meth.NoCheckMiddleTierRole();
			return SecretValue.Read(ProgramProperties.GetValOrDefault(programNum,desc,clinicNum),legacyPolicy);
		}

		///<summary>Encrypts plainText and stores it in the row for programNum, desc and clinicNum, inserting the row if needed (masked, high security).
		///Returns false without writing anything if encryption fails. Does not refresh the cache; the caller signals InvalidType.Programs.</summary>
		public static bool WriteSecret(long programNum,string desc,long clinicNum,string plainText) {
			Meth.NoCheckMiddleTierRole();
			string stored;
			if(!SecretValue.Protect(plainText,out stored)) {
				return false;
			}
			ProgramProperty programProperty=EnsureSecret(programNum,desc,clinicNum);
			if(programProperty.PropertyValue==stored) {
				return true;
			}
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=stored;
			ProgramProperties.Update(programProperty,programPropertyOld);
			return true;
		}

		///<summary>Stores a plain setting, inserting the row if needed. Does not refresh the cache.</summary>
		public static void WriteSetting(long programNum,string desc,long clinicNum,string value) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty programProperty=EnsureSetting(programNum,desc,clinicNum);
			if(programProperty.PropertyValue==(value??"")) {
				return;
			}
			ProgramProperty programPropertyOld=programProperty.Copy();
			programProperty.PropertyValue=value??"";
			ProgramProperties.Update(programProperty,programPropertyOld);
		}

		///<summary>A configuration fingerprint for TokenCache: the program, clinic and the stored (encrypted) values of the given rows, read from the
		///cache. A changed setting gives a new fingerprint, so a stale access token is never reused.</summary>
		public static string GetFingerprint(long programNum,long clinicNum,params string[] arrayDescs) {
			Meth.NoCheckMiddleTierRole();
			string[] arrayParts=new string[arrayDescs.Length+2];
			arrayParts[0]=POut.Long(programNum);
			arrayParts[1]=POut.Long(clinicNum);
			for(int i=0;i<arrayDescs.Length;i++) {
				arrayParts[i+2]=ProgramProperties.GetValOrDefault(programNum,arrayDescs[i],clinicNum);
			}
			return TokenCache.Fingerprint(arrayParts);
		}
	}

	///<summary>A refresh token kept in one programproperty row, encrypted with SecretValue. ReadCurrent reads the database, not the cache, so a token
	///another workstation rotated is seen at once. TryReplace is a compare-and-swap on the ciphertext (osd1$ is deterministic). Plain-text values
	///are treated per legacyPolicy (RejectPlaintext for tokens issued under Open Dental).</summary>
	public sealed class ProgramPropertyRefreshTokenStore : IRefreshTokenStore {
		private readonly long _programNum;
		private readonly string _desc;
		private readonly long _clinicNum;
		private readonly LegacyPolicy _legacyPolicy;

		public ProgramPropertyRefreshTokenStore(long programNum,string desc,long clinicNum=0,LegacyPolicy legacyPolicy=LegacyPolicy.RejectPlaintext) {
			_programNum=programNum;
			_desc=desc;
			_clinicNum=clinicNum;
			_legacyPolicy=legacyPolicy;
		}

		public string ReadCurrent() {
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(_programNum,_desc,_clinicNum);
			if(programProperty==null) {
				return "";
			}
			return SecretValue.ReadOrBlank(programProperty.PropertyValue,_legacyPolicy);
		}

		public bool TryReplace(string expectedCurrent,string newValue) {
			ProgramProperty programProperty=ProgramProperties.GetOneFromDb(_programNum,_desc,_clinicNum);
			if(programProperty==null) {
				return false;
			}
			string storedNew;
			if(!SecretValue.Protect(newValue,out storedNew)) {
				return false;
			}
			//Compare against what is stored now, after checking it still decrypts to expectedCurrent.
			if(SecretValue.ReadOrBlank(programProperty.PropertyValue,_legacyPolicy)!=(expectedCurrent??"")) {
				return false;
			}
			return ProgramProperties.UpdateValueIfUnchanged(programProperty.ProgramPropertyNum,programProperty.PropertyValue,storedNew);
		}
	}
}
