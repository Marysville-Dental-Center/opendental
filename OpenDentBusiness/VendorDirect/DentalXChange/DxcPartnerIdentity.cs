// DentalXChange: the partner identity (Client and Service ID) that DentalXChange's SOAP web services and claim upload expect next to the office's
// clearinghouse login. DentalXChange issues it to the practice-management software it works with. Open Dental's own values used to be compiled in;
// this fork compiles in nothing. The office enters what DentalXChange issued to it, and until it does every call that needs the identity is off.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Reflection;
using CodeBase;

namespace OpenDentBusiness.VendorDirect.DentalXChange {
	///<summary>The office's DentalXChange partner identity, kept in two preference rows that are not in the PrefName enum (like the fork migration
	///runner's rows): ForkDxcPartnerClient and ForkDxcPartnerServiceID. Rows are inserted the first time the office saves a value, so an adopted Open
	///Dental database needs no migration and reads as blank. Blank means "not set up": DentalXChange calls that send the identity (claim upload,
	///eligibility, ERA download, payer list, attachments through the partner service, claim validation through the DWS service) refuse to run and
	///make no network call. XConnect calls send the office's own API key instead and do not need it.</summary>
	public class DxcPartnerIdentity {
		public const string PREF_CLIENT="ForkDxcPartnerClient";
		public const string PREF_SERVICE_ID="ForkDxcPartnerServiceID";

		///<summary>The partner name DentalXChange issued (sent as Client, and as UploaderName on claim upload).</summary>
		public string Client="";
		///<summary>The DCI web service ID DentalXChange issued, exactly as issued.</summary>
		public string ServiceID="";

		public DxcPartnerIdentity() {
		}

		public DxcPartnerIdentity(string client,string serviceID) {
			Client=(client??"").Trim();
			ServiceID=(serviceID??"").Trim();
		}

		///<summary>True when both values are set. Nothing that needs the identity runs otherwise.</summary>
		public bool IsConfigured {
			get {
				return !string.IsNullOrWhiteSpace(Client) && !string.IsNullOrWhiteSpace(ServiceID);
			}
		}

		///<summary>The identity from the preference cache. Missing rows read as blank. Never throws.</summary>
		public static DxcPartnerIdentity GetCur() {
			Meth.NoCheckMiddleTierRole();
			return new DxcPartnerIdentity(GetPrefOrBlank(PREF_CLIENT),GetPrefOrBlank(PREF_SERVICE_ID));
		}

		///<summary>The identity, or an ODException with GetNotConfiguredMessage() when it is not set up. Call before any DentalXChange call that sends
		///it, so an office without its own identity never sends a blank or borrowed one.</summary>
		public static DxcPartnerIdentity GetConfiguredOrThrow() {
			Meth.NoCheckMiddleTierRole();
			DxcPartnerIdentity identity=GetCur();
			if(!identity.IsConfigured) {
				throw new ODException(GetNotConfiguredMessage());
			}
			return identity;
		}

		public static string GetNotConfiguredMessage() {
			return Lans.g("ClaimConnect","DentalXChange is not set up: enter the partner Client and Service ID that DentalXChange issued to this office in "
				+"Setup > Family/Insurance > Clearinghouses, on the ClaimConnect clearinghouse.");
		}

		///<summary>Stores both values (trimmed), inserting the rows if needed, and updates this workstation's cache. Returns true if anything changed.
		///The caller signals InvalidType.Prefs so other workstations reload.</summary>
		public static bool Save(string client,string serviceID) {
			Meth.NoCheckMiddleTierRole();
			DxcPartnerIdentity identityOld=GetCur();
			DxcPartnerIdentity identityNew=new DxcPartnerIdentity(client,serviceID);
			if(identityOld.Client==identityNew.Client && identityOld.ServiceID==identityNew.ServiceID) {
				return false;
			}
			UpsertPrefs(identityNew.Client,identityNew.ServiceID);
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_CLIENT,ValueString=identityNew.Client });
			Prefs.UpdateValueForKey(new Pref { PrefName=PREF_SERVICE_ID,ValueString=identityNew.ServiceID });
			return true;
		}

		///<summary>Writes both preference rows straight to the table, inserting missing ones.</summary>
		public static void UpsertPrefs(string client,string serviceID) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),client,serviceID);
				return;
			}
			UpsertPref(PREF_CLIENT,client??"");
			UpsertPref(PREF_SERVICE_ID,serviceID??"");
		}

		private static void UpsertPref(string prefName,string value) {
			string count=Db.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(PIn.Long(count)==0) {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(prefName)+"','"+POut.String(value)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(prefName)+"'");
		}

		private static string GetPrefOrBlank(string prefName) {
			try {
				if(Prefs.DictIsNull() || !Prefs.GetContainsKey(prefName)) {
					return "";
				}
				return (Prefs.GetOne(prefName).ValueString??"").Trim();
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}
	}
}
