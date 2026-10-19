// USPS address validation: the address check that Edit Patient and form import call, and the USPS Addresses v3 response it returns.
// Re-implements, in this repository, the surface that the prebuilt Bridges.dll provided (inferred from this repository's callers only).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using CodeBase;
using CodeBase.VendorDirect;
using Newtonsoft.Json;

namespace OpenDentBusiness {
	///<summary>Checks one patient address with USPS, using the office's own USPS app (program link USPSAddresses). Single addresses only, as a user
	///saves or imports them: USPS's terms forbid batch clean-up and building address lists, so never add a "verify all" feature on top of this.</summary>
	public static class USPSAddressValidationDatas {
		///<summary>Sends the street, unit, city, state and ZIP of address (never a name) to USPS and returns the standardized address.
		///Returns a response with address==null and ErrorMessage set when USPS finds no match, or when the address doesn't have enough to check
		///(no call is made then). Throws UspsApiException when USPS can't be reached, refuses the credentials, is over the quota, or the program link
		///isn't configured. Runs on the client (never the middle tier server). Blocks: call it from a worker thread (ProgressWin).</summary>
		public static USPSAddressValidationResponse GetUSPSAddressValidationResponse(Address address) {
			Meth.NoCheckMiddleTierRole();
			UspsConfig uspsConfig=UspsAddresses.GetConfig();
			if(!Programs.IsEnabled(ProgramName.USPSAddresses) || !uspsConfig.IsComplete) {
				throw new UspsApiException(UspsErrorKind.NotConfigured,0,"","USPS address validation is not set up.");
			}
			string errorMessage;
			UspsAddressQuery uspsAddressQuery=UspsAddressQuery.FromAddress(address,out errorMessage);
			if(uspsAddressQuery==null) {
				USPSAddressValidationResponse response=new USPSAddressValidationResponse();
				response.ErrorMessage=errorMessage;
				return response;
			}
			return VendorHttp.RunSync(() => UspsApiClient.Default.GetAddressAsync(uspsConfig,uspsAddressQuery,CancellationToken.None));
		}

		///<summary>For the setup window's Test connection: asks USPS for a new token with uspsConfig (not yet saved), then checks one fixed, public,
		///non-patient address (USPS headquarters). Returns a short result to show. Throws UspsApiException. Costs one call from the office's quota
		///and one billed lookup. Blocks: call it from a worker thread.</summary>
		public static string TestConnection(UspsConfig uspsConfig) {
			Meth.NoCheckMiddleTierRole();
			UspsApiClient.Default.Reset(uspsConfig);
			OAuthAccessToken token=VendorHttp.RunSync(() => UspsApiClient.Default.GetAccessTokenAsync(uspsConfig,CancellationToken.None));
			Address address=new Address();
			address.Address1="475 L'Enfant Plaza SW";
			address.City="Washington";
			address.State="DC";
			address.Zip="20260";
			string errorMessage;
			UspsAddressQuery uspsAddressQuery=UspsAddressQuery.FromAddress(address,out errorMessage);
			USPSAddressValidationResponse response=VendorHttp.RunSync(
				() => UspsApiClient.Default.GetAddressAsync(uspsConfig,uspsAddressQuery,CancellationToken.None));
			//The credentials work now: forget earlier rejections and the quota pause for the saved settings too.
			UspsApiClient.Default.ResetAll();
			UspsAddresses.ClearQuotaCooldown();
			string scope=string.IsNullOrWhiteSpace(token.Scope) ? "" : " (scope: "+token.Scope.Trim()+")";
			if(response.address==null) {
				return "Connected"+scope+", but USPS did not return the test address: "+response.ErrorMessage;
			}
			return "Connected"+scope+". Test address: "+response.address.streetAddress+", "+response.address.city+" "+response.address.state+" "
				+response.address.ZIPCode+(string.IsNullOrWhiteSpace(response.address.ZIPPlus4) ? "" : "-"+response.address.ZIPPlus4);
		}
	}

	///<summary>The USPS Addresses v3 AddressResponse. Public fields keep the API's JSON names (ZIPCode and ZIPPlus4 keep their case), so Newtonsoft
	///binds them with no attributes. Unknown fields are ignored.</summary>
	public class USPSAddressValidationResponse {
		///<summary>The business name at the address, if USPS has one. Never sent by this program.</summary>
		public string firm;
		///<summary>The standardized address, or null when USPS found no match.</summary>
		public USPSAddressValidationAddress address;
		///<summary>Delivery point details, including DPVConfirmation.</summary>
		public USPSAddressValidationAdditionalInfo additionalInfo;
		///<summary>USPS correction codes, e.g. 32 (more detail such as a unit is needed) or 22 (several addresses matched).</summary>
		public List<USPSAddressValidationCodeText> corrections;
		///<summary>USPS match codes, e.g. 31 (exact single match).</summary>
		public List<USPSAddressValidationCodeText> matches;
		///<summary>Free-text warnings from USPS.</summary>
		public List<string> warnings;
		///<summary>Not from the JSON. Why there is no address: USPS's message for a 404 (no match), or why no call was made.</summary>
		[JsonIgnore]
		public string ErrorMessage;
	}

	///<summary>The address part of a USPS Addresses v3 response. Upper case, USPS standard.</summary>
	public class USPSAddressValidationAddress {
		///<summary>Address1. May already include the unit, with secondaryAddress null.</summary>
		public string streetAddress;
		public string streetAddressAbbreviation;
		///<summary>Address2 (APT, STE...). Can be null even when the input had a unit.</summary>
		public string secondaryAddress;
		public string cityAbbreviation;
		public string city;
		public string state;
		public string urbanization;
		///<summary>Five digits.</summary>
		public string ZIPCode;
		///<summary>Four digits, or null.</summary>
		public string ZIPPlus4;
	}

	///<summary>The additionalInfo part of a USPS Addresses v3 response. Only DPVConfirmation is shown; the rest is kept for later use.</summary>
	public class USPSAddressValidationAdditionalInfo {
		public string deliveryPoint;
		public string carrierRoute;
		///<summary>Y: confirmed (with the unit, if given). D: building confirmed, unit missing. S: building confirmed, unit given didn't confirm.
		///N: not confirmed. Y alone doesn't mean USPS delivers there.</summary>
		public string DPVConfirmation;
		///<summary>From the enhanced release: adds R (confirmed, but USPS doesn't deliver there). Not shown yet.</summary>
		public string DPVEnhancedConfirmation;
		public string DPVCMRA;
		public string DPVVacant;
		public string vacant;
		public string centralDeliveryPoint;
	}

	///<summary>A code and its text, as in USPS's matches and corrections lists.</summary>
	public class USPSAddressValidationCodeText {
		public string code;
		public string text;
	}

	///<summary>USPS's DPV confirmation codes. The member names are the codes, so Enum.Parse works on the response value. The descriptions are this
	///program's own wording, shown in red in Address Compare for anything other than Y.</summary>
	public enum EnumDPVConfirmation {
		///<summary>No code, or a code this program doesn't know.</summary>
		[Description("USPS could not confirm this address is deliverable.")]
		None,
		///<summary>Confirmed.</summary>
		[Description("")]
		Y,
		[Description("USPS confirmed the building but the apartment/suite number is missing.")]
		D,
		[Description("USPS confirmed the building but not the apartment/suite number entered.")]
		S,
		[Description("USPS could not confirm this street number.")]
		N,
	}
}
