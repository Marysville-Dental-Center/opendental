using CodeBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenDentBusiness {
	///<summary>The PDMP (LogiCoy) and Bamboo program links: a prescription monitoring check from the patient's chart. Both talk to the vendor directly
	///with the office's own credentials (docs/specs/vendor-direct/pdmp.md). Both programs are disabled until the office enables them.</summary>
	public class PDMP {
		///<summary>Text for the user. For Bamboo, the scores and messages to show before the report is requested.</summary>
		public string Message;
		///<summary>The report URL to open. For LogiCoy it is set by SendData. For Bamboo it is set by GetBambooReportLink, after the user clicks OK.</summary>
		public string Url;
		///<summary>A warning to show the user, such as a client certificate that expires soon. "" when none.</summary>
		public string Warning="";
		///<summary>The Bamboo session between step 1 (SendData) and step 2 (GetBambooReportLink). Null for LogiCoy.</summary>
		private PDMPBamboo _pdmpBamboo;

		/// <summary>List of programs that can access PDMP bridge</summary>
		private static List<string> _listPDMPProgNames=new List<string>() {
			ProgramName.PDMP.ToString(),
			ProgramName.Bamboo.ToString(),
		};

		///<summary>True if the program is one of the two PDMP program links.</summary>
		public static bool IsPdmpProgram(Program program) {
			return program!=null && _listPDMPProgNames.Contains(program.ProgName);
		}

		///<summary>True for Bamboo when step 1 returned a report that GetBambooReportLink can open.</summary>
		public bool HasBambooReport {
			get {
				return _pdmpBamboo!=null && _pdmpBamboo.HasReport;
			}
		}

		///<summary>Validates the program properties and runs the first vendor call. LogiCoy: the report URL is in Url. Bamboo: only the patient query
		///runs; the scores are in Message, and GetBambooReportLink asks for the report once the user has confirmed. Throws ApplicationException
		///(PdmpException) with a message for the user.</summary>
		public static PDMP SendData(Program programCur,Patient pat,Provider prov) {
			PDMP pdmp=new PDMP();
			StringBuilder sbErrors=new StringBuilder();
			if(!programCur.Enabled) {
				sbErrors.AppendLine(programCur.ProgName+Lans.g("PDMP"," must be enabled in Program Links."));
				throw new Exception(sbErrors.ToString());
			}
			if(prov==null) {
				sbErrors.AppendLine(Lans.g("PDMP","Logged in user must be associated with a provider to access this feature."));
				throw new ApplicationException(sbErrors.ToString());
			}
			if(pat==null) {
				sbErrors.AppendLine(Lans.g("PDMP","Please select a patient."));
				throw new ApplicationException(sbErrors.ToString());
			}
			PdmpProperty.EnsureProperties(programCur);
			PdmpProperty propertyVals=new PdmpProperty() {
				PdmpProv=prov,
				PdmpPat=pat,
				StateAbbr=ProviderClinics.GetStateWhereLicensed(prov.ProvNum,Clinics.ClinicNum),//Will be validated below
				Dea=ProviderClinics.GetDEANum(prov.ProvNum,Clinics.ClinicNum)//If no result found, retries using clinicNum=0. Will be validated below.
			};
			try {
				propertyVals.LoadPropertiesForProgram(programCur,Clinics.ClinicNum);
				propertyVals.Validate(programCur);
			}
			catch(Exception ex) {
				sbErrors.AppendLine(ex.Message);
			}
			//Validation failed.  We gave the user as much information to fix as possible.
			if(!string.IsNullOrWhiteSpace(sbErrors.ToString())) {
				throw new ApplicationException(sbErrors.ToString());
			}
			//Validation passed and we can now call the PDMP API.
			//We have multiple PDMP program links and there's overlap to which states they cover so we'll use different classes to manage the bridges
			if(Enum.TryParse(programCur.ProgName,out ProgramName programName)) {
				switch(programName) {
					case ProgramName.PDMP:
						PDMPLogicoy pdmpLogicoy=new PDMPLogicoy(propertyVals);
						pdmp.Url=pdmpLogicoy.GetURL();
						break;
					case ProgramName.Bamboo:
						PDMPBamboo pdmpBamboo=new PDMPBamboo(propertyVals);
						pdmpBamboo.QueryPatient();
						pdmp._pdmpBamboo=pdmpBamboo;
						pdmp.Message=pdmpBamboo.Response;
						pdmp.Warning=pdmpBamboo.CertificateWarning;
						break;
					default:
						throw new Exception(Lans.g("PDMP","PDMP program link has not been implemented."));
				}
			}
			else {
				throw new Exception(Lans.g("PDMP","Could not parse PDMP program name."));
			}
			return pdmp;
		}

		///<summary>Bamboo step 2, called only after the user clicks OK: requests the single-use report link, stores it in Url and returns it.
		///Throws PdmpException.</summary>
		public string GetBambooReportLink() {
			if(_pdmpBamboo==null) {
				throw new PdmpException(PdmpErrorKind.VendorError,Lans.g("PDMP","No report was returned."));
			}
			Url=_pdmpBamboo.GetReportLink();
			return Url;
		}

		///<summary>Orders the program's properties for the setup grid: the license field first, then the rest. The URL rows are shown: each office enters
		///the URL its vendor gave it. For Bamboo, the client certificate file and its password are hidden; the certificate dialog manages them and the
		///setup window shows one status row instead. Rows of other programs pass through unchanged.</summary>
		public static List<ProgramProperty> FilterAndSortProperties(Program progCur,List<ProgramProperty> listProps) {
			if(!_listPDMPProgNames.Contains(progCur.ProgName)) {
				return listProps;//Program isn't PDMP so we don't care
			}
			List<ProgramProperty> retList=new List<ProgramProperty>();
			retList.AddRange(listProps.Where(x=>x.PropertyDesc==PdmpProperty.PdmpProvLicenseField));//add license type first
			if(Enum.TryParse(progCur.ProgName,out ProgramName programName)) {
				switch(programName) {
					case ProgramName.PDMP:
						retList.AddRange(listProps.Where(x=>x.PropertyDesc!=PdmpProperty.PdmpProvLicenseField)//already added license above
							.OrderBy(x=>x.PropertyDesc));//order by state name
						break;
					case ProgramName.Bamboo:
						retList.AddRange(listProps.Where(x=>x.PropertyDesc!=PdmpProperty.BambooAuthToken
							&& x.PropertyDesc!=PdmpProperty.BambooAuthPassword//managed by the certificate dialog
							&& x.PropertyDesc!=PdmpProperty.PdmpProvLicenseField));//already added above
						break;
					default:
						throw new ApplicationException(Lans.g("PDMP","Could not parse program name."));
				}
			}
			return retList;
		}
	}
}
