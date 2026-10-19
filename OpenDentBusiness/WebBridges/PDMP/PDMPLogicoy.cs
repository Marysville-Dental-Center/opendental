using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using CodeBase;
using CodeBase.VendorDirect;

namespace OpenDentBusiness {
	///<summary>Talks to LogiCoy PMPlink directly from the workstation: one XML POST to the office's per-state URL with HTTP Basic auth (the office's
	///LogiCoy web-service username and password). The answer carries a report URL that opens in the program's browser window.</summary>
	public class PDMPLogicoy {
		private PdmpProperty _pdmpProperties;
		private PDMPScript.USStateCodeBaseType _stateCode;
		private readonly HttpMessageHandler _httpMessageHandler;

		///<summary>httpMessageHandler replaces the network in tests; otherwise props.HttpMessageHandlerOverride, else the real network.</summary>
		public PDMPLogicoy(PdmpProperty props,HttpMessageHandler httpMessageHandler=null) {
			_pdmpProperties=props;
			_httpMessageHandler=httpMessageHandler??props.HttpMessageHandlerOverride;
			Enum.TryParse(_pdmpProperties.StateAbbr,out _stateCode);
		}

		///<summary>Submits a request to the appropriate API and returns a string containing the URL supplied by the API. Throws PdmpException.</summary>
		public string GetURL() {
			PDMPScript.PrescriptionSummaryRequestType request=new PDMPScript.PrescriptionSummaryRequestType();
			request.Requester=MakeRequester();
			request.PrescriptionRequest=MakePrescriptionRequest();
			Dictionary<string,string> dictHeaders=new Dictionary<string,string>() {
				{ "Authorization",MakeBasicAuthHeader() },
			};
			PDMPScript.PrescriptionSummaryResponseType response=PdmpHttp.PostXml<PDMPScript.PrescriptionSummaryResponseType>(_pdmpProperties.Url,
				PdmpHttp.Serialize(request),dictHeaders,null,_httpMessageHandler);
			return ProcessResponse(response);
		}

		///<summary>Reads the response: the report URL, checked against the vendor URL policy, or a PdmpException with the vendor's error. Public for tests.</summary>
		public static string ProcessResponse(PDMPScript.PrescriptionSummaryResponseType response) {
			if(response==null) {
				throw new PdmpException(PdmpErrorKind.MalformedResponse,PdmpHttp.MessageMalformed);
			}
			if(response.Error!=null) {
				StringBuilder stringBuilder=new StringBuilder();
				if(!string.IsNullOrWhiteSpace(response.Error.Message)) {
					stringBuilder.AppendLine(response.Error.Message);
				}
				if(response.Error.Details!=null && response.Error.Details.Length>0) {
					stringBuilder.AppendLine(Lans.g("PDMP","Details:")+" "+string.Join("\n",response.Error.Details));
				}
				if(stringBuilder.Length==0) {
					stringBuilder.AppendLine(Lans.g("PDMP","The service returned an error without a message."));
				}
				throw new PdmpException(PdmpErrorKind.VendorError,stringBuilder.ToString());//Request resulted in an error.
			}
			string url=response.Report?.ReportRequestURLs?.ViewableReport?.Value?.Trim()??"";
			if(url=="") {
				throw new PdmpException(PdmpErrorKind.VendorError,Lans.g("PDMP","No report was returned."));
			}
			VendorUrlProblem problem=VendorUrlPolicy.Check(url);
			if(problem!=VendorUrlProblem.None) {
				throw new PdmpException(PdmpErrorKind.UrlRefused,Lans.g("PDMP","The report address was refused.")+" "+VendorUrlPolicy.Describe(problem));
			}
			return url;
		}

		///<summary>The SenderSoftware identity comes from the override rows, else this product. Never Open Dental's name.</summary>
		public PDMPScript.RequesterType MakeRequester() {
			//There is no documentation identifying what the use case of "RoleType" is.  Setting to Dentist for now seems like the safe choice.
			PDMPScript.RoleType role=PDMPScript.RoleType.Dentist;
			string[] arrRequestDestinations=new string[] { _pdmpProperties.StateAbbr, };
			PDMPScript.RequesterType requester=new PDMPScript.RequesterType {
				SenderSoftware=new PDMPScript.SenderSoftware {
					Developer=_pdmpProperties.SenderDeveloper,
					Product=_pdmpProperties.SenderProduct,
					Version=_pdmpProperties.SenderVersion,
				},
				RequestDestinations=arrRequestDestinations,
				Provider=new PDMPScript.Provider {
					Role=role,
					FirstName=_pdmpProperties.PdmpProv.FName,
					LastName=_pdmpProperties.PdmpProv.LName,
					DEANumber=new PDMPScript.DEANumberType {
						Value=_pdmpProperties.Dea,
					},
					NPINumber=new PDMPScript.NPINumberType {
						Value=_pdmpProperties.PdmpProv.NationalProvID,
					},
					ProfessionalLicenseNumber=new PDMPScript.ProfessionalLicenseNumber {
						Value=new PDMPScript.LimitedStringType {
							Value=_pdmpProperties.ProvLicenseNum,
						},
					},
					LocationStateCode=new PDMPScript.USStateCodeType {
						Value=_stateCode,
					},
				},
				Location=new PDMPScript.Location {
					Name=_pdmpProperties.FacilityId,
					DEANumber=new PDMPScript.DEANumberType {
						Value=_pdmpProperties.Dea,
					},
					NPINumber=new PDMPScript.NPINumberType {
						Value=_pdmpProperties.PdmpProv.NationalProvID,
					},
				},
			};
			return requester;
		}

		///<summary>The patient's SSN is sent only to states whose rules require it (PdmpProperty.RequiresPatientSsn).</summary>
		public PDMPScript.PrescriptionSummaryRequestTypePrescriptionRequest MakePrescriptionRequest() {
			PDMPScript.SexCodeBaseType sex;
			switch(_pdmpProperties.PdmpPat.Gender) {
				case PatientGender.Female:
					sex=PDMPScript.SexCodeBaseType.F;
					break;
				case PatientGender.Male:
					sex=PDMPScript.SexCodeBaseType.M;
					break;
				case PatientGender.Unknown:
				default:
					sex=PDMPScript.SexCodeBaseType.U;
					break;
			}
			string ssn=null;
			if(PdmpProperty.RequiresPatientSsn(_pdmpProperties.StateAbbr) && !string.IsNullOrWhiteSpace(_pdmpProperties.PdmpPat.SSN)) {
				ssn=_pdmpProperties.PdmpPat.SSN;
			}
			PDMPScript.PrescriptionSummaryRequestTypePrescriptionRequest prescrRequest=new PDMPScript.PrescriptionSummaryRequestTypePrescriptionRequest {
				Patient=new PDMPScript.RequestPatientType {
					Name=new PDMPScript.RequestPatientTypeName {
						First=new PDMPScript.LimitedStringType {
							Value=_pdmpProperties.PdmpPat.FName,
						},
						Last=new PDMPScript.LimitedStringType {
							Value=_pdmpProperties.PdmpPat.LName,
						},
					},
					Birthdate=_pdmpProperties.PdmpPat.Birthdate,
					SexCode=new PDMPScript.SexCodeType {
						Value=sex,
					},
					Items=new object[1] {
						new PDMPScript.AddressRequiredZipType(){
							Street=new PDMPScript.LimitedStringType[] {
								new PDMPScript.LimitedStringType {
									Value=_pdmpProperties.PdmpPat.Address,
								},
							},
							City=new PDMPScript.LimitedStringType {
								Value=_pdmpProperties.PdmpPat.City
							},
							ZipCode=new PDMPScript.ZipCodeType {
								Value=_pdmpProperties.PdmpPat.Zip
							},
						},
					},
					SSN=ssn,
				},

			};
			return prescrRequest;
		}

		///<summary>HTTP Basic with the office's LogiCoy credentials, ISO-8859-1 as before.</summary>
		private string MakeBasicAuthHeader() {
			string basicAuthContent=Convert.ToBase64String(Encoding.GetEncoding("ISO-8859-1").GetBytes((_pdmpProperties.Username??"")+":"+(_pdmpProperties.Password??"")));
			return "Basic "+basicAuthContent;
		}
	}
}
