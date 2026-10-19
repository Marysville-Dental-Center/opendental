using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Bridges;
using CodeBase;
using Newtonsoft.Json;
using WebServiceSerializer;

namespace OpenDentBusiness {
	public class WebServiceMainHQMockDemo : OpenDentBusiness.WebServiceMainHQ.WebServiceMainHQ, IWebServiceMainHQ {
		public Func<string,string> EnableAdditionalFeaturesDelegate;
		public Func<string,string> LogCareCreditTransactionsDelegate;
		///<summary>Client id for Google's sample application found here: https://github.com/googlesamples/oauth-apps-for-windows/tree/master/OAuthDesktopApp
		///The sample application is not scoped to send emails or view the inbox, so you can get tokens but you can't do anything with them.</summary>
		private const string CLIENT_ID_GOOGLE_SAMPLE_APP="581786658708-elflankerquo1a6vsckabbhn25hclla0.apps.googleusercontent.com";
		///<summary>Client secret for Google's sample application found here: https://github.com/googlesamples/oauth-apps-for-windows/tree/master/OAuthDesktopApp
		///The sample application is not scoped to send emails or view the inbox, so you can get tokens but you can't do anything with them.</summary>
		private const string CLIENT_SECRET_GOOGLE_SAMPLE_APP="3f6NggMbPtrmIBpgx-MK2xXK";

		public List<long> GetEServiceClinicsAllowed(List<long> listClinicNums,eServiceCode eService) {
			throw new NotImplementedException();
		}

		public new string EnableAdditionalFeatures(string officeData) {
			if(EnableAdditionalFeaturesDelegate is null) {
				return base.EnableAdditionalFeatures(officeData);
			}
			return EnableAdditionalFeaturesDelegate(officeData);
		}

		public new string LogCareCreditTransaction(string officeData) {
			if(LogCareCreditTransactionsDelegate is null) {
				return base.EnableAdditionalFeatures(officeData);
			}
			return LogCareCreditTransactionsDelegate(officeData);

		}

		public new string LicenseAgreementAccepted(string officeData) {
			return WebSerializer.SerializePrimitive(true);
		}

		public new string BuildOAuthUrl(string registrationKey,string appName) {
			try {
				if(!Enum.TryParse<OAuthApplicationNames>(appName,out OAuthApplicationNames appNameEnum)) {
					throw new ApplicationException("Could not parse OAuthApplicationName: "+appName);
				}
				string retVal="";
				switch(appNameEnum) {
					case OAuthApplicationNames.Dropbox:
						throw new NotImplementedException("GetDropBoxAuthorizationUrl() is not implemented in WebServiceMainHQMockDemo.cs");
						//retVal=GetDropboxAuthorizationUrl(eAccount.RegistrationKeyNum);
						break;
					case OAuthApplicationNames.Google:
						retVal=GetGoogleAuthorizationUrl(-1);
						break;
					case OAuthApplicationNames.GoogleLoopbackIpAddressFlow:
						retVal=GetGoogleAuthorizationUrlLoopbackIpAddressFlow(-1);
						break;
					case OAuthApplicationNames.QuickBooksOnline:
						//Doesn't return a URL here. Instead returns the Client ID and Secret which is used to build an OAuth request.
						throw new NotImplementedException("Use introspection to test QuickBooks Online. See the Introspection wiki.");
						//retVal=QUICKBOOKSONLINE_CLIENT_ID+":"+QUICKBOOKSONLINE_CLIENT_SECRET;
						break;
					case OAuthApplicationNames.PayConnect2:
					case OAuthApplicationNames.PayConnect2Dll:
						throw new NotImplementedException("Use introspection to test PayConnect2. See the Introspection wiki.");
					default:
						throw new ApplicationException("Could not find OAuthApplicationName: "+appName);
				}
				return retVal;
			}
			catch(Exception ex) {
				return PayloadHelper.CreateErrorResponse(ex,"Error occured while attempting to build OAuth URL.");
			}
		}

		public new string GetGoogleAccessToken(string officeData) {
			string body;
			try {
				#region Extract Method Specific Vairables from officeData
				string grant=WebSerializer.DeserializeTag<string>(officeData,"Code");
				bool isRefreshToken=WebSerializer.DeserializeTag<bool>(officeData,"IsRefreshToken");
				//These two nodes are for Google's OAuth Loopback IP Address flow that we switched to because they depricated Out-Of-Band (OOB) flow.
				//If these two nodes are not found, we don't throw and assume that this method is being called by an older version of OD trying to use OOB flow.
				//The redirectUri points to a port on the customer's computer. It is fine to send this to WebServiceMainHQ
				//because Google just compares it to the redirectUri that was sent with the previous auth code request as an additional security measure.
				string redirectUri=WebSerializer.DeserializeNode(officeData,"RedirectUri",doThrowIfNotFound:false);
				string codeVerifier=WebSerializer.DeserializeNode(officeData,"CodeVerifier",doThrowIfNotFound:false);
				#endregion
				//Uses the client id and secret for Google's sample application found here: https://github.com/googlesamples/oauth-apps-for-windows/tree/master/OAuthDesktopApp
				//The sample application is not scoped to send emails or view the inbox, so you can get tokens but you can't do anything with them.
				if(isRefreshToken) {
					body=$"client_id={CLIENT_ID_GOOGLE_SAMPLE_APP}&client_secret={CLIENT_SECRET_GOOGLE_SAMPLE_APP}&grant_type=refresh_token&refresh_token={grant}";
				}
				else {//Getting access token via authentication code
							//Try to use the old OOB flow if either of these were not included in the officeData.
					if(redirectUri.IsNullOrEmpty() || codeVerifier.IsNullOrEmpty()) {
						body=$"client_id={CLIENT_ID_GOOGLE_SAMPLE_APP}&client_secret={CLIENT_SECRET_GOOGLE_SAMPLE_APP}&grant_type=authorization_code&code={grant}&redirect_uri=urn:ietf:wg:oauth:2.0:oob";
					}
					else {//Use the Loopback IP Address flow when redirectUri and codeVerifier are present.
						body=$"client_id={CLIENT_ID_GOOGLE_SAMPLE_APP}&client_secret={CLIENT_SECRET_GOOGLE_SAMPLE_APP}&grant_type=authorization_code&"+
							$"code={grant}&redirect_uri={Uri.EscapeDataString(redirectUri)}&code_verifier={codeVerifier}";
					}
				}
				var resObj=Google.GetAccessToken(body);
				return JsonConvert.SerializeObject(resObj);
			}
			catch(Exception ex) {
				return JsonConvert.SerializeObject(new GoogleToken("","",ex.Message));
			}
		}

		public new string GetGoogleAuthorizationUrl(long registrationKeyNum) {
			try {
				return JsonConvert.SerializeObject(@$"client_id={CLIENT_ID_GOOGLE_SAMPLE_APP}&scope=openid%20profile&access_type=offline&redirect_uri=urn:ietf:wg:oauth:2.0:oob&response_type=code");
			}
			catch(Exception ex) {
				return PayloadHelper.CreateErrorResponse(ex,"Error occured while attempting to retrieve OAuth URL.");
			}
		}

		public new string GetGoogleAuthorizationUrlLoopbackIpAddressFlow(long registrationKeyNum) {
			try {
				return JsonConvert.SerializeObject(@$"client_id={CLIENT_ID_GOOGLE_SAMPLE_APP}&scope=openid%20profile&access_type=offline&response_type=code");
			}
			catch(Exception ex) {
				return PayloadHelper.CreateErrorResponse(ex,"Error occured while attempting to retrieve OAuth URL.");
			}
		}

		public string GetPostcardManiaSSO(string officeData) {
			PostcardManiaSSOResponse res=new PostcardManiaSSOResponse();
			res.SSO="URL";
			return PayloadHelper.CreateSuccessResponse(res,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public string ManageAdvertisingPostcardsAccount(string officeData) {
			PostcardManiaAccountCreateResponse res=new PostcardManiaAccountCreateResponse();
			res.AccountId=1;
			res.Company="Mordor Inc";
			res.Email="mock@example.invalid";
			return PayloadHelper.CreateSuccessResponse(res,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public string UploadPostcardManiaPatientList(string officeData) {
			PostcardManiaUploadPatientsResponse res=new PostcardManiaUploadPatientsResponse();
			res.ListID=1;
			res.Success=true;
			return PayloadHelper.CreateSuccessResponse(res,AdvertisingPostcards.MassPostcardResponseNode);
		}

		public string GetAdvertisingPostcardsAccounts(string officeData) {
			PostcardManiaMetaData res=new PostcardManiaMetaData();
			return PayloadHelper.CreateSuccessResponse(res,AdvertisingPostcards.MassPostcardResponseNode);
		}

		/// <summary>This is never run in demo mode currently. 
		/// It does not run because the demo checkin does not contain any ImageCaptureXs with non-misc image capture types. 
		/// A signup is also required.</summary>
		public string ProcessOCRIIDRequest(string imgRequest) {
			throw new NotImplementedException();
		}

		/// <summary> Returning null is the result of a failure to process the request. </summary>
		public string ProcessUSPSAddressValidationRequest(string payload) {
			return null;
		}
	}
}
