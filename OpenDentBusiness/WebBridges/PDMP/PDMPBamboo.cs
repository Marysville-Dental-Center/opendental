using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using BambooScript;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness {
	/// <summary>Formerly Appriss. Talks to the Bamboo Health PMP Gateway directly from the workstation, with the office's own Gateway credentials and
	///client certificate. Two steps: QueryPatient (the PatientRequest) runs before the user sees the scores; GetReportLink (the ReportRequest) runs only
	///after the user clicks OK, so Cancel never asks the PMP for a report and the single-use link can't expire while the message box is open.</summary>
	public class PDMPBamboo {
		private PdmpProperty _pdmpProperties;
		private USStateCodeType  _stateCode;
		private SexCodeType _patGender;
		private readonly HttpMessageHandler _httpMessageHandler;
		///<summary>The single-use report link, set by GetReportLink. Open it at once and never reuse it.</summary>
		public string Url;
		///<summary>The text for the message box after QueryPatient: scores and messages.</summary>
		public string Response;
		///<summary>The ViewableReport URL from QueryPatient, where GetReportLink posts. "" for a message-only report (no match, for example).</summary>
		public string ViewableReportUrl="";
		///<summary>Set when the client certificate expires within 30 days.</summary>
		public string CertificateWarning="";

		///<summary>httpMessageHandler replaces the network in tests; otherwise props.HttpMessageHandlerOverride, else the real network.</summary>
		public PDMPBamboo(PdmpProperty props,HttpMessageHandler httpMessageHandler=null) {
			_pdmpProperties=props;
			_httpMessageHandler=httpMessageHandler??props.HttpMessageHandlerOverride;
			Enum.TryParse(_pdmpProperties.StateAbbr,out _stateCode);
			_patGender=GetPatGender(_pdmpProperties.PdmpPat);
		}

		///<summary>True if QueryPatient returned a report that GetReportLink can open.</summary>
		public bool HasReport {
			get {
				return !string.IsNullOrWhiteSpace(ViewableReportUrl);
			}
		}

		///<summary>Returns BambooScript.SexCodeType. Defaults to 'Unknown'</summary>
		private SexCodeType GetPatGender(Patient pat) {
			SexCodeType gender=SexCodeType.U;
			if(pat==null) {
				return gender;
			}
			if(pat.Gender==PatientGender.Male) {
				gender=SexCodeType.M;
			}
			else if(pat.Gender==PatientGender.Female) {
				gender=SexCodeType.F;
			}
			return gender;
		}

		///<summary>Step 1: sends the PatientRequest and fills Response and ViewableReportUrl. Throws PdmpException.</summary>
		public void QueryPatient() {
			PatientRequestType request=MakeRequester();
			request.PrescriptionRequest=MakePrescriptionRequest();
			PatientResponseType patientResponse=Request<PatientResponseType>(PdmpHttp.Serialize(request),_pdmpProperties.Url);
			ProcessPatientResponse(patientResponse);
		}

		///<summary>Reads a PatientResponse. A report sets Response (and ViewableReportUrl when the report can be opened). Disallowed and Error items throw
		///PdmpException with the vendor's message. Public for tests.</summary>
		public void ProcessPatientResponse(PatientResponseType patientResponse) {
			if(patientResponse==null) {
				throw new PdmpException(PdmpErrorKind.MalformedResponse,PdmpHttp.MessageMalformed);
			}
			if(patientResponse.Item is ReportType report) {
				ViewableReportUrl=GetViewableReportUrl(report);
				Response=GetResponse(report,ViewableReportUrl);
				return;
			}
			if(patientResponse.Item is PatientResponseTypeError error) {
				throw new PdmpException(PdmpErrorKind.VendorError,GetErrorResponse(error.Message,error.Details,error.Source,error.SourceSpecified));
			}
			if(patientResponse.Response!=null) {
				ResponseTypeDisallowed disallowed=patientResponse.Response.Select(x => x?.Item).OfType<ResponseTypeDisallowed>().FirstOrDefault();
				if(disallowed!=null) {
					throw new PdmpException(PdmpErrorKind.Disallowed,
						GetErrorResponse(disallowed.Message,disallowed.Details,disallowed.Source,disallowed.SourceSpecified)+PdmpHttp.MessageSearchPortal);
				}
				ResponseTypeError responseError=patientResponse.Response.Select(x => x?.Item).OfType<ResponseTypeError>().FirstOrDefault();
				if(responseError!=null) {
					throw new PdmpException(PdmpErrorKind.VendorError,
						GetErrorResponse(responseError.Message,responseError.Details,responseError.Source,responseError.SourceSpecified));
				}
			}
			throw new PdmpException(PdmpErrorKind.MalformedResponse,PdmpHttp.MessageMalformed);
		}

		///<summary>The ViewableReport URL of a report, or "" when the report carries none (a message-only report). Never throws.</summary>
		public static string GetViewableReportUrl(ReportType report) {
			return report?.ReportRequestURLs?.ViewableReport?.Value?.Trim()??"";
		}

		private string GetErrorResponse(string message,string[] details,ReportResponseTypeErrorSource source,bool isSourceSpecified) {
			StringBuilder strErr=new StringBuilder();
			if(!string.IsNullOrWhiteSpace(message)) {
				strErr.AppendLine(message);
			}
			if(details!=null && details.Length>0) {
				strErr.AppendLine(Lans.g("Bamboo","Details:")+" "+string.Join("\n",details));
			}
			if(isSourceSpecified && !string.IsNullOrWhiteSpace(strErr.ToString())) {
				strErr.AppendLine(Lans.g("Bamboo","Source:")+" "+source.ToString());
			}
			if(strErr.Length==0) {
				strErr.AppendLine(Lans.g("Bamboo","The service returned an error without a message."));
			}
			return strErr.ToString();
		}

		///<summary>Creates response for pop up to display before user can continue on to the returned link. The hierarchy of perferred responses is as follows:
		///	1. Narx Scores if present, plus any messages included in the report
		///	2. The message in the report. Testing shows that if multiple matches are found for a patient then it will let the user know that this the case and they will need to log into Bamboo directly.
		///	3. A resonse indicating that no patients have been found in the Bamboo database that match the selected patients.
		///	Note: This method is public for testing purposes only
		///</summary>
		public string GetResponse(ReportType report,string url) {
			string strFurtherActionNeeded=Lans.g("Bamboo","Search for the selected patient on your state PMP website.");
			StringBuilder retVal=new StringBuilder();
			retVal.AppendLine(Lans.g("Bamboo","Risk assessment for ")+_pdmpProperties.PdmpPat.LName+", "+_pdmpProperties.PdmpPat.FName);
			if(report.NarxScores!=null) {
				NarxScoreType[] narxMessage=report.NarxScores;
				for(int i=0;i<narxMessage.Length;i++) {
					if(narxMessage[i]==null) {
						continue;
					}
					retVal.AppendLine(narxMessage[i].ScoreType+": "+narxMessage[i].ScoreValue);
				}
				if(!report.Message.IsNullOrEmpty()) {
					retVal.AppendLine(report.Message);
				}
				else {
					retVal.AppendLine(Lans.g("Bamboo","Some states do not require the full PDMP report to be viewed."));
				}
			}
			else if (!report.Message.IsNullOrEmpty()) {
				retVal.AppendLine(report.Message);
				retVal.AppendLine(strFurtherActionNeeded);
			}
			else {
				retVal.AppendLine(Lans.g("Bamboo","No matches found for the selected patient."));
				retVal.AppendLine(strFurtherActionNeeded);
			}
			if(!string.IsNullOrEmpty(url)) {
				retVal.AppendLine(Lans.g("Bamboo","Click 'OK' to continue on to the full report. Clicking 'Cancel' will result in an audit log not being created."));
			}
			return retVal.ToString();
		}

		///<summary>Step 2, called only after the user clicks OK: posts the ReportRequest to the ViewableReport URL and returns the single-use report link
		///(also stored in Url). The link is checked against the vendor URL policy before it is returned. Throws PdmpException.</summary>
		public string GetReportLink() {
			if(!HasReport) {
				throw new PdmpException(PdmpErrorKind.VendorError,Lans.g("Bamboo","No report was returned."));
			}
			ReportRequestType reportRequest=MakeReportRequest();
			ReportResponseType reportResponse=Request<ReportResponseType>(PdmpHttp.Serialize(reportRequest),ViewableReportUrl);
			Url=ProcessReportResponse(reportResponse);
			return Url;
		}

		///<summary>Reads a ReportResponse: returns the ReportLink, or throws PdmpException with the vendor's Error or Disallowed message. Public for tests.</summary>
		public string ProcessReportResponse(ReportResponseType reportResponse) {
			object[] arrayItems=reportResponse?.Items??new object[0];
			//Items array from report response could return several different items. From the documentation it looks like only one would show up.
			//The url will show up as a string and any other response is it's own type in the Bamboo script that indicates some sort of error.
			string link=arrayItems.OfType<string>().FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
			if(link!=null) {
				link=link.Trim();
				VendorUrlProblem problem=VendorUrlPolicy.Check(link);
				if(problem!=VendorUrlProblem.None) {
					throw new PdmpException(PdmpErrorKind.UrlRefused,Lans.g("Bamboo","The report address was refused.")+" "+VendorUrlPolicy.Describe(problem));
				}
				return link;
			}
			ReportResponseTypeDisallowed disallowed=arrayItems.OfType<ReportResponseTypeDisallowed>().FirstOrDefault();
			if(disallowed!=null) {
				throw new PdmpException(PdmpErrorKind.Disallowed,
					GetErrorResponse(disallowed.Message,disallowed.Details,disallowed.Source,disallowed.SourceSpecified)+PdmpHttp.MessageSearchPortal);
			}
			ReportResponseTypeError error=arrayItems.OfType<ReportResponseTypeError>().FirstOrDefault();
			if(error!=null) {
				throw new PdmpException(PdmpErrorKind.VendorError,GetErrorResponse(error.Message,error.Details,error.Source,error.SourceSpecified));
			}
			throw new PdmpException(PdmpErrorKind.MalformedResponse,PdmpHttp.MessageMalformed);
		}

		#region Requesters
		///<summary>Method is public for testing purposes only.</summary>
		public PatientRequestTypePrescriptionRequest MakePrescriptionRequest() {
			PatientRequestTypePrescriptionRequest prescrRequest=new PatientRequestTypePrescriptionRequest {
				Patient=new BambooScript.PatientType {
					Name=new PatientTypeName {
						First=_pdmpProperties.PdmpPat.FName,
						Middle=_pdmpProperties.PdmpPat.MiddleI,
						Last=_pdmpProperties.PdmpPat.LName,
						},
					Birthdate=_pdmpProperties.PdmpPat.Birthdate,
					SexCode=_patGender,
					Items=new object[1] {
						new AddressRequiredZipType(){
							Street=new string[] {
								_pdmpProperties.PdmpPat.Address,
							},
							City=_pdmpProperties.PdmpPat.City,
							StateCodeSpecified=true,//This value needed to be set in order for the state code to show up in the prescription request
							StateCode=_stateCode,
							ZipCode=_pdmpProperties.PdmpPat.Zip
						},
					},
				},
			};
			return prescrRequest;
		}

		///<summary>Method is public for testing purposes only. The ReportRequest's requester has no SenderSoftware in schema v5_1.</summary>
		public ReportRequestType MakeReportRequest() {
			ReportRequestType reportRequest=new ReportRequestType();
			reportRequest.Requester=new ReportRequestTypeRequester {
				Provider=new ProviderTypeForReports {
					Role=BambooScript.RoleType.Dentist,
					FirstName=_pdmpProperties.PdmpProv.FName,
					LastName=_pdmpProperties.PdmpProv.LName,
					NPINumber=_pdmpProperties.PdmpProv.NationalProvID,
					DEANumber=_pdmpProperties.Dea,
					ProfessionalLicenseNumber=new ProfessionalLicenseNumberType {
						Type=_pdmpProperties.ProvLicenseType,
						Value=_pdmpProperties.ProvLicenseNum,
						StateCode=_stateCode,
					}
				},
				Location=MakeLocation(),
			};
			return reportRequest;
		}

		///<summary>The requesting location: the facility name, the facility's identifiers (else the provider's), and the clinic's or practice's address.
		///Never the patient's address.</summary>
		private BambooScript.LocationType MakeLocation() {
			USStateCodeType stateCodeLocation=_stateCode;
			if(!string.IsNullOrWhiteSpace(_pdmpProperties.LocationStateAbbr)
				&& Enum.TryParse(_pdmpProperties.LocationStateAbbr.Trim().ToUpperInvariant(),out USStateCodeType stateCodeParsed))
			{
				stateCodeLocation=stateCodeParsed;
			}
			LocationTypeAddress locationTypeAddress=new LocationTypeAddress {
				StateCode=stateCodeLocation,
			};
			if(!string.IsNullOrWhiteSpace(_pdmpProperties.LocationStreet)) {
				locationTypeAddress.Street=new string[] { _pdmpProperties.LocationStreet };
			}
			if(!string.IsNullOrWhiteSpace(_pdmpProperties.LocationCity)) {
				locationTypeAddress.City=_pdmpProperties.LocationCity;
			}
			if(!string.IsNullOrWhiteSpace(_pdmpProperties.LocationZip)) {
				locationTypeAddress.ZipCode=_pdmpProperties.LocationZip;
			}
			return new BambooScript.LocationType {
				Name=_pdmpProperties.FacilityId,
				DEANumber=string.IsNullOrWhiteSpace(_pdmpProperties.LocationDea) ? _pdmpProperties.Dea : _pdmpProperties.LocationDea,
				NPINumber=string.IsNullOrWhiteSpace(_pdmpProperties.LocationNpi) ? _pdmpProperties.PdmpProv.NationalProvID : _pdmpProperties.LocationNpi,
				Address=locationTypeAddress,
			};
		}
		#endregion

		///<summary>Posts body to url with the digest headers and the client certificate. The certificate is loaded per call and disposed after it.
		///Throws PdmpException.</summary>
		private T Request<T>(string body,string url) {
			BambooAuth auth=new BambooAuth(BambooAuth.GetEpoch(DateTime.Now),BambooAuth.GetNonce());
			Dictionary<string,string> dictHeaders=new Dictionary<string,string>() {
				{ "X-Auth-PasswordDigest",auth.GetPasswordDigest(_pdmpProperties.Password??"") },
				{ "X-Auth-Nonce",auth.Nonce },
				{ "X-Auth-Timestamp",auth.EpochTime.ToString() },
				{ "X-Auth-Username",_pdmpProperties.Username??"" },
			};
			X509Certificate2 certificate=null;
			try {
				//A test handler never sees a certificate. In the program, the handler is always null and testing mode is off.
				if(_httpMessageHandler==null && !Introspection.IsTestingMode) {
					certificate=PdmpCertificate.Load(_pdmpProperties,DateTime.UtcNow);
					CertificateWarning=PdmpCertificate.GetExpiryWarning(certificate,DateTime.UtcNow);
				}
				return PdmpHttp.PostXml<T>(url,body,dictHeaders,certificate,_httpMessageHandler,isDigestAuth:true);
			}
			finally {
				certificate?.Dispose();
			}
		}

		///<summary>Method is public for testing purposes only. The Requester carries the SenderSoftware identity (the override rows, else this product,
		///never Open Dental).</summary>
		public PatientRequestType MakeRequester() {
			//There is no documentation identifying what the use case of "RoleType" is.  Setting to Dentist for now seems like the safe choice.
			BambooScript.RoleType role=BambooScript.RoleType.Dentist;
			PatientRequestType requester=new PatientRequestType {
				Requester=new RequesterType {
					SenderSoftware=new RequesterTypeSenderSoftware {
						Developer=_pdmpProperties.SenderDeveloper,
						Product=_pdmpProperties.SenderProduct,
						Version=_pdmpProperties.SenderVersion,
					},
					Provider=new ProviderType {
						Role=role,
						FirstName=_pdmpProperties.PdmpProv.FName,
						LastName=_pdmpProperties.PdmpProv.LName,
						DEANumber=_pdmpProperties.Dea,
						NPINumber=_pdmpProperties.PdmpProv.NationalProvID,
						ProfessionalLicenseNumber=new ProfessionalLicenseNumberType {
							Type=_pdmpProperties.ProvLicenseType,
							Value=_pdmpProperties.ProvLicenseNum,
							StateCode=_stateCode
						},
					},
					Location=MakeLocation(),
				},
			};
			return requester;
		}
	}

	public partial class BambooAuth {
		public readonly long EpochTime;
		public readonly string Nonce;

		public BambooAuth(long epoch,string nonce) {
			EpochTime=epoch;
			Nonce=nonce;
		}

		///<summary>Documentation indicates that part of the password digest needs to be Epoch time in seconds</summary>
		public static long GetEpoch(DateTime dt) {
			return (long)dt.ToUniversalTime().Subtract(new DateTime(1970,1,1)).TotalSeconds;
		}

		///<summary>According to Bamboo documentation, the nonce cannot have ":" in it as that would mess up the hashing</summary>
		public static string GetNonce() {
			return Guid.NewGuid().ToString().Replace(":","");
		}

		///<summary>Password digest is needed to get authorization to access site: lowercase hex SHA-256 of the UTF-8 bytes of password:nonce:timestamp.</summary>
		public string GetPasswordDigest(string password) {
			string strPlainText=$"{password}:{Nonce}:{EpochTime}";
			using(SHA256 sha256=SHA256.Create()) {
				return ToHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(strPlainText)));
			}
		}

		///<summary>Bamboo requires a hex encoded SHA hash</summary>
		private static string ToHex(byte[] bytes) {
			return string.Join("",bytes.Select(x => x.ToString("x2")));
		}
	}
}
