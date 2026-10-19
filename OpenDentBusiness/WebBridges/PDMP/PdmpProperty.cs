using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using CodeBase;
using CodeBase.VendorDirect;
using OpenDentBusiness.VendorDirect;

namespace OpenDentBusiness {
	///<summary>A collection of specific properties for PDMP links. Used to more easily pass information around.
	///Settings live in the programproperty rows of the PDMP (LogiCoy) and Bamboo programs. Each office enters its own values in Setup > Program Links;
	///missing rows are inserted at runtime by EnsureProperties. Reads are per clinic with a fallback to the ClinicNum 0 row. Secrets are osd1$ values;
	///anything else in a secret row was written by Open Dental and is never sent (docs/specs/vendor-direct/pdmp.md, section 3.2).</summary>
	public class PdmpProperty {
		#region PDMP properties
		public const string PdmpUserName="PDMP Username";
		public const string PdmpPassword="PDMP Password";
		public const string PdmpUrl="PDMP Url";
		public const string PdmpFacilityID="PDMP FacilityID";
		public const string PdmpProvLicenseField="PDMP Provider License Field";
		///<summary>Overrides the SenderSoftware Developer sent to LogiCoy. Blank means the built-in product identity.</summary>
		public const string PdmpSenderDeveloper="PDMP Sender Developer";
		///<summary>Overrides the SenderSoftware Product sent to LogiCoy. Blank means the built-in product identity.</summary>
		public const string PdmpSenderProduct="PDMP Sender Product";
		#endregion
		#region Bamboo properties
		///<summary>The office's PKCS#12 client certificate, base64, osd1$. Written only by the certificate dialog.</summary>
		public const string BambooAuthToken="Bamboo Client Key";
		///<summary>The password of the PKCS#12 file, osd1$. Written only by the certificate dialog.</summary>
		public const string BambooAuthPassword="Bamboo Client Password";
		public const string BambooUserName="Bamboo Username";
		public const string BambooPassword="Bamboo Password";
		public const string BambooUrl= "Bamboo Url";
		public const string BambooFacilityID="Bamboo FacilityID";
		///<summary>Optional. A certificate in the Windows store (CurrentUser\My, then LocalMachine\My) to use instead of an imported file.</summary>
		public const string BambooClientCertThumbprint="Bamboo Client Certificate Thumbprint";
		///<summary>Overrides the SenderSoftware Developer sent to Bamboo. Blank means the built-in product identity.</summary>
		public const string BambooSenderDeveloper="Bamboo Sender Developer";
		///<summary>Overrides the SenderSoftware Product sent to Bamboo. Blank means the built-in product identity.</summary>
		public const string BambooSenderProduct="Bamboo Sender Product";
		///<summary>Optional. The facility's NPI, sent as Location/NPINumber. Blank sends the provider's NPI.</summary>
		public const string BambooFacilityNpi="Bamboo Facility NPI";
		///<summary>Optional. The facility's DEA number, sent as Location/DEANumber. Blank sends the provider's DEA number.</summary>
		public const string BambooFacilityDea="Bamboo Facility DEA";
		///<summary>The decrypted base64 PKCS#12 file, or "" when not set (or when the row holds a value Open Dental wrote).</summary>
		public string BambooClientKey;
		///<summary>The decrypted PKCS#12 password, or "".</summary>
		public string BambooClientPassword;
		#endregion
		public string FacilityId;
		public string Password;
		public string Username;
		public string ProvLicenseType;
		public string ProvLicenseNum;
		public string Url;
		public string Dea;
		public string StateAbbr;
		public Provider PdmpProv;
		public Patient PdmpPat;
		///<summary>The clinic the settings were read for. 0 is the practice (default) rows.</summary>
		public long ClinicNum;
		///<summary>The program's ProgName, for messages.</summary>
		public string ProgName="";
		///<summary>Thumbprint of a Windows store certificate (Bamboo). Upper-case hex, or "".</summary>
		public string ClientCertThumbprint="";
		///<summary>True when the password row holds a value this program can't read (Open Dental's ciphertext, or a corrupted value).</summary>
		public bool IsPasswordReentryNeeded;
		///<summary>True when the client certificate rows hold a value Open Dental wrote. Treated as not set.</summary>
		public bool IsClientCertLegacy;
		///<summary>Override row values; blank means the built-in identity.</summary>
		public string SenderDeveloperOverride="";
		public string SenderProductOverride="";
		///<summary>The location sent to Bamboo: the clinic's address, else the practice's. Never the patient's.</summary>
		public string LocationStreet="";
		public string LocationCity="";
		public string LocationStateAbbr="";
		public string LocationZip="";
		///<summary>The facility identifiers sent to Bamboo. Blank falls back to the provider's NPI and DEA.</summary>
		public string LocationNpi="";
		public string LocationDea="";
		///<summary>Replaces the network in tests. Null in the program.</summary>
		public HttpMessageHandler HttpMessageHandlerOverride;

		///<summary>The LogiCoy states this bridge has rows for, by abbreviation. Keep alphabetical.</summary>
		public static readonly ReadOnlyDictionary<string,string> DictLogicoyStates=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>() {
			{ "CA","California" },
			{ "IL","Illinois" },
			{ "KY","Kentucky" },
			{ "MD","Maryland" },
			{ "UT","Utah" },
			{ "WA","Washington" },
		});

		#region Sender identity
		///<summary>SenderSoftware Developer: the override row, else the built-in product identity. Never Open Dental's name.</summary>
		public string SenderDeveloper {
			get {
				return string.IsNullOrWhiteSpace(SenderDeveloperOverride) ? ProductIdentity.ProductName : SenderDeveloperOverride.Trim();
			}
		}

		///<summary>SenderSoftware Product: the override row, else the built-in product identity.</summary>
		public string SenderProduct {
			get {
				return string.IsNullOrWhiteSpace(SenderProductOverride) ? ProductIdentity.ProductName : SenderProductOverride.Trim();
			}
		}

		///<summary>SenderSoftware Version: always the program's version.</summary>
		public string SenderVersion {
			get {
				return ProductIdentity.Version;
			}
		}
		#endregion Sender identity

		#region Rows
		///<summary>The practice (ClinicNum 0) rows a program needs, with whether each is a secret.</summary>
		public static List<Tuple<string,bool,string>> GetRowDefinitions(string progName) {
			Meth.NoCheckMiddleTierRole();
			List<Tuple<string,bool,string>> listRows=new List<Tuple<string,bool,string>>();
			if(progName==ProgramName.Bamboo.ToString()) {
				listRows.Add(Tuple.Create(BambooUrl,false,""));
				listRows.Add(Tuple.Create(BambooUserName,false,""));
				listRows.Add(Tuple.Create(BambooPassword,true,""));
				listRows.Add(Tuple.Create(BambooFacilityID,false,""));
				listRows.Add(Tuple.Create(BambooFacilityNpi,false,""));
				listRows.Add(Tuple.Create(BambooFacilityDea,false,""));
				listRows.Add(Tuple.Create(BambooAuthToken,true,""));
				listRows.Add(Tuple.Create(BambooAuthPassword,true,""));
				listRows.Add(Tuple.Create(BambooClientCertThumbprint,false,""));
				listRows.Add(Tuple.Create(BambooSenderDeveloper,false,""));
				listRows.Add(Tuple.Create(BambooSenderProduct,false,""));
				listRows.Add(Tuple.Create(PdmpProvLicenseField,false,nameof(ProviderClinic.StateLicense)));
			}
			else if(progName==ProgramName.PDMP.ToString()) {
				foreach(string stateFull in DictLogicoyStates.Values) {
					listRows.Add(Tuple.Create(GetPropDesc(stateFull,PdmpUrl),false,""));
					listRows.Add(Tuple.Create(GetPropDesc(stateFull,PdmpUserName),false,""));
					listRows.Add(Tuple.Create(GetPropDesc(stateFull,PdmpPassword),true,""));
					listRows.Add(Tuple.Create(GetPropDesc(stateFull,PdmpFacilityID),false,""));
				}
				listRows.Add(Tuple.Create(PdmpSenderDeveloper,false,""));
				listRows.Add(Tuple.Create(PdmpSenderProduct,false,""));
				listRows.Add(Tuple.Create(PdmpProvLicenseField,false,nameof(ProviderClinic.StateLicense)));
			}
			return listRows;
		}

		///<summary>True for rows that hold a secret (osd1$, masked, high security).</summary>
		public static bool IsSecretDesc(string desc) {
			Meth.NoCheckMiddleTierRole();
			return desc==BambooAuthToken || desc==BambooAuthPassword || (desc??"").EndsWith("Password");
		}

		///<summary>Inserts the practice (ClinicNum 0) rows the program needs that are missing: blank values, and secrets masked and high security.
		///Never changes an existing value. Refreshes the cache and signals other workstations if anything changed. Returns true if anything changed.
		///Runs when the setup window opens and before every use, so a workstation never fails because nobody has opened Setup yet.</summary>
		public static bool EnsureProperties(Program program) {
			Meth.NoCheckMiddleTierRole();
			if(program==null) {
				return false;
			}
			bool isChanged=false;
			List<ProgramProperty> listProgramProperties=ProgramProperties.GetWhere(x => x.ProgramNum==program.ProgramNum && x.ClinicNum==0);
			foreach(Tuple<string,bool,string> row in GetRowDefinitions(program.ProgName)) {
				ProgramProperty programProperty=listProgramProperties.FirstOrDefault(x => x.PropertyDesc==row.Item1);
				bool isSecret=row.Item2;
				if(programProperty!=null && (!isSecret || (programProperty.IsMasked && programProperty.IsHighSecurity))) {
					continue;
				}
				if(isSecret) {
					VendorCredentials.EnsureSecret(program.ProgramNum,row.Item1,0);
				}
				else {
					VendorCredentials.EnsureSetting(program.ProgramNum,row.Item1,0,row.Item3);
				}
				isChanged=true;
			}
			if(isChanged) {
				ProgramProperties.RefreshCache();
				Signalods.SetInvalid(InvalidType.Programs);
			}
			return isChanged;
		}

		///<summary>True if any secret row of the program (any clinic) holds a value this program can't use: Open Dental's ciphertext or plain text.
		///The setup window then marks the URLs as carried over.</summary>
		public static bool HasLegacyValues(long programNum) {
			Meth.NoCheckMiddleTierRole();
			return ProgramProperties.GetWhere(x => x.ProgramNum==programNum && IsSecretDesc(x.PropertyDesc))
				.Any(x => SecretValue.Read(x.PropertyValue,LegacyPolicy.RejectPlaintext).NeedsReentry);
		}

		///<summary>Reads a secret for the clinic (else the practice row). Values without osd1$ read as blank; needsReentry is then true.</summary>
		private static string ReadSecret(long programNum,string desc,long clinicNum,out bool needsReentry) {
			SecretReadResult secretReadResult=VendorCredentials.ReadSecret(programNum,desc,clinicNum,LegacyPolicy.RejectPlaintext);
			needsReentry=secretReadResult.NeedsReentry;
			return secretReadResult.PlainText;
		}

		private static string GetVal(long programNum,string desc,long clinicNum) {
			return ProgramProperties.GetValOrDefault(programNum,desc,clinicNum).Trim();
		}
		#endregion Rows

		#region Methods used by both bridges
		///<summary>Delegate function that determines necessary values for each state are set for provider and patient</summary>
		private delegate bool PropertyValidator(PdmpProperty prop,out string error);
		private static readonly Dictionary<string,List<PropertyValidator>> _dictStateRequirements=new Dictionary<string,List<PropertyValidator>>() {
			{ "CA", new List<PropertyValidator>{TryValidateDea,TryValidateProvName,TryValidatePatBirthday,TryValidateUrl } },
			{ "IL", new List<PropertyValidator>{TryValidateDea,TryValidateUrl,TryValidatePatBirthday } },
			{ "KY", new List<PropertyValidator>{TryValidateDea,TryValidateProvName,TryValidatePatSsn,TryValidatePatBirthday,TryValidateUrl } },
			{ "MD", new List<PropertyValidator>{TryValidateDea,TryValidateProvName,TryValidateProvLicense,TryValidatePatAddress,TryValidatePatBirthday ,TryValidateUrl} },
			{ "UT", new List<PropertyValidator>{TryValidateDea,TryValidateProvName,TryValidatePatBirthday,TryValidateUrl } },
			{ "WA", new List<PropertyValidator>{TryValidateProvName,TryValidateProvLicense,TryValidatePatBirthday,TryValidateUrl } }
		};

		///<summary>True if the LogiCoy state requires the patient's SSN (KY today). The SSN is sent only to those states.</summary>
		public static bool RequiresPatientSsn(string stateAbbr) {
			Meth.NoCheckMiddleTierRole();
			return _dictStateRequirements.TryGetValue(stateAbbr??"",out List<PropertyValidator> listValidators)
				&& listValidators.Contains(TryValidatePatSsn);
		}

		///<summary>Uses current program and clinic num to get properties for whichever pdmp bridge user is connecting to.</summary>
		public void LoadPropertiesForProgram(Program programCur,long clinicNum) {
			ClinicNum=clinicNum;
			ProgName=programCur.ProgName;
			//both programs need this set ahead of time
			ProvLicenseType=GetVal(programCur.ProgramNum,PdmpProvLicenseField,clinicNum);
			if(string.IsNullOrWhiteSpace(ProvLicenseType)) {
				ProvLicenseType=nameof(ProviderClinic.StateLicense);
			}
			bool useRxID=DoUseRxID();
			ProvLicenseNum=ProviderClinics.GetStateLicenseForProv(PdmpProv.ProvNum,StateAbbr,clinicNum,useRxID);
			if(programCur.ProgName==ProgramName.PDMP.ToString()) {
				LoadPDMPProps(programCur.ProgramNum,clinicNum);
			}
			else if(programCur.ProgName==ProgramName.Bamboo.ToString()) {
				LoadBambooProps(programCur.ProgramNum,clinicNum);
				LoadLocation(clinicNum);
			}
			if(Introspection.IsTestingMode) {
				LoadDebugOverrides(programCur);
			}
		}

		/// <summary>Returns true if the providers State Rx ID should be used instead of the State License Number, false otherwise.</summary>
		public bool DoUseRxID() {
			if(string.IsNullOrWhiteSpace(ProvLicenseType)) {
				return false;
			}
			if(ProvLicenseType==nameof(ProviderClinic.StateRxID)) {
				return true;
			}
			return false;
		}

		///<summary>Fills the Location fields from the clinic's address, else the practice's. Never the patient's.</summary>
		public void LoadLocation(long clinicNum) {
			Clinic clinic=(clinicNum==0) ? null : Clinics.GetClinic(clinicNum);
			if(clinic!=null && !string.IsNullOrWhiteSpace(clinic.Address)) {
				LocationStreet=clinic.Address;
				LocationCity=clinic.City;
				LocationStateAbbr=clinic.State;
				LocationZip=clinic.Zip;
				return;
			}
			LocationStreet=PrefC.GetString(PrefName.PracticeAddress);
			LocationCity=PrefC.GetString(PrefName.PracticeCity);
			LocationStateAbbr=PrefC.GetString(PrefName.PracticeST);
			LocationZip=PrefC.GetString(PrefName.PracticeZip);
		}

		///<summary>Checks each PDMP program bridge and makes sure we have everything we need to initiate a connection with website. Can throw</summary>
		public void Validate(Program progCur) {
			string error="";
			if(PdmpPat is null) {
				error=Lans.g("PDMP","No patient selected.\r\n");
			}
			if(PdmpProv is null) {
				error+=Lans.g("PDMP","User is not associated with a valid provider.\r\n");
			}
			if(!string.IsNullOrWhiteSpace(error)) {
				//Something prior to this has gone horribly wrong since all of the above information should have already been validated.
				throw new ODException(error.ToString());
			}
			if(string.IsNullOrEmpty(ProgName)) {
				ProgName=progCur.ProgName;
			}
			if(progCur.ProgName==ProgramName.PDMP.ToString()) {
				error=ValidatePDMP();
			}
			else if(progCur.ProgName==ProgramName.Bamboo.ToString()) {
				error=ValidateBamboo();
			}
			if(!error.IsNullOrEmpty()) {
				throw new ODException(error);
			}
		}

		///<summary>Private validate method takes in a list of delegates and makes sure neccessary fields are set. If there is no issue with the properties being passed in then an empty string is returned</summary>
		private string Validate(List<PropertyValidator> listValidators) {
			StringBuilder err=new StringBuilder();
			foreach(PropertyValidator validator in listValidators) {
				if(!validator(this,out string propErr)) {
					err.AppendLine(propErr);
				}
			}
			return err.ToString();
		}

		///<summary>"Set it in Setup > Program Links > {program}."</summary>
		private string GetSetupHint() {
			return Lans.g("PDMP","Set it in Setup > Program Links >")+" "+(string.IsNullOrEmpty(ProgName) ? Lans.g("PDMP","this program link") : ProgName)+".";
		}

		private static bool TryValidateDea(PdmpProperty prop,out string error) {
			error="";
			StringBuilder deaErr=new StringBuilder();
			if(prop.Dea.IsNullOrEmpty()) {
				deaErr.AppendLine(Lans.g("PDMP","DEA number is required for provider."));
			}
			else if(prop.Dea.Length!=9) {
				deaErr.AppendLine(Lans.g("PDMP","DEA number must be comprised of 9 alphanumeric characters."));
			}
			error=deaErr.ToString();
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidateProvName(PdmpProperty prop, out string error) {
			error="";
			if(prop.PdmpProv.FName.IsNullOrEmpty() || prop.PdmpProv.LName.IsNullOrEmpty()) {
			 error=Lans.g("PDMP","Provider must have a first and last name.");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidateProvLicense(PdmpProperty prop, out string error) {
			error="";
			string rxIDOrLicenseNumber="";
			if(prop.ProvLicenseNum.IsNullOrEmpty()) {
				if(prop.DoUseRxID()) {
					rxIDOrLicenseNumber="Rx ID";
				}
				else {
					rxIDOrLicenseNumber="License Number";
				}
				error=Lans.g("PDMP",$"Provider's State {rxIDOrLicenseNumber} is required.");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidatePatSsn(PdmpProperty prop,out string error) {
			error="";
			if(prop.PdmpPat.SSN.IsNullOrEmpty()) {
				error=Lans.g("PDMP","Patient's social security number is required.");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidatePatAddress(PdmpProperty prop,out string error) {
			Patient pat=prop.PdmpPat;
			error="";
			if(pat.Address.IsNullOrEmpty() || pat.City.IsNullOrEmpty() || pat.State.IsNullOrEmpty()) {
				error=Lans.g("PDMP","Patient's full address is required");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidatePatBirthday(PdmpProperty prop,out string error) {
			error="";
			if(prop.PdmpPat.Birthdate.Year<=1880) {
				error=Lans.g("PDMP","Patient's birthday is invalid.");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		///<summary>The URL must be set, absolute HTTPS (plain http only to this computer), and not on a host this program never contacts.</summary>
		public static bool TryValidateUrl(PdmpProperty prop,out string error) {
			string url=prop.Url;
			error="";
			if(url.IsNullOrEmpty()) {
				error=Lans.g("PDMP","Program url was not set.")+" "+prop.GetSetupHint();
				return false;
			}
			VendorUrlProblem problem=VendorUrlPolicy.Check(url);
			if(problem!=VendorUrlProblem.None) {
				error=Lans.g("PDMP","Program url is not valid.")+" "+VendorUrlPolicy.Describe(problem)+" "+prop.GetSetupHint();
			}
			return string.IsNullOrWhiteSpace(error);
		}

		///<summary>Username and password must be set. A password another program saved can't be read and must be re-entered.</summary>
		public static bool TryValidateCredentials(PdmpProperty prop,out string error) {
			error="";
			if(Introspection.IsTestingMode) {
				return true;
			}
			if(prop.IsPasswordReentryNeeded) {
				error=Lans.g("PDMP","The saved password can't be used by this program. Re-enter the password.")+" "+prop.GetSetupHint();
			}
			else if(string.IsNullOrWhiteSpace(prop.Username) || string.IsNullOrEmpty(prop.Password)) {
				error=Lans.g("PDMP","Username and password are required.")+" "+prop.GetSetupHint();
			}
			return string.IsNullOrWhiteSpace(error);
		}

		private static bool TryValidatePatZip(PdmpProperty prop, out string error) {
			error="";
			if(prop.PdmpPat.Zip.IsNullOrEmpty()) {
				error=Lans.g("PDMP","Patient's zipcode is invalid");
			}
			return string.IsNullOrWhiteSpace(error);
		}

		public static bool TryValidateState(PdmpProperty prop,out string error) {
			error="";
			if(!Regex.IsMatch(prop.StateAbbr??"",
				"^(?:(A[KLRZ]|C[AOT]|D[CE]|FL|GA|HI|I[ADLN]|K[SY]|LA|M[ADEINOST]|N[CDEHJMVY]|O[HKR]|P[AR]|RI|S[CD]|T[NX]|UT|V[AIT]|W[AIVY]))$"))
			{
				error=Lans.g("PDMP","State Where Licensed is invalid for user's provider.");
			}
			return string.IsNullOrWhiteSpace(error);
		}
		#endregion

		#region Logicoy methods
		///<summary>Loads properties for Logicoy PDMP for the clinic, falling back to the practice rows.</summary>
		private void LoadPDMPProps(long progNum,long clinicNum) {
			string stateFull=GetStateFull();
			FacilityId=GetVal(progNum,GetPropDesc(stateFull,PdmpFacilityID),clinicNum);
			Username=GetVal(progNum,GetPropDesc(stateFull,PdmpUserName),clinicNum);
			Password=ReadSecret(progNum,GetPropDesc(stateFull,PdmpPassword),clinicNum,out IsPasswordReentryNeeded);
			Url=GetVal(progNum,GetPropDesc(stateFull,PdmpUrl),clinicNum);
			SenderDeveloperOverride=GetVal(progNum,PdmpSenderDeveloper,clinicNum);
			SenderProductOverride=GetVal(progNum,PdmpSenderProduct,clinicNum);
		}

		///<summary>Logicoy requires different information from state to state and so extra validation needs to be made on its behalf. See documentation</summary>
		private string ValidatePDMP() {
			if(!_dictStateRequirements.TryGetValue(StateAbbr??"",out List<PropertyValidator> listValidators)) {
				return (Lans.g("PDMP","Invalid state abbreviation.\r\n"));
			}
			List<PropertyValidator> listAll=new List<PropertyValidator>(listValidators);
			listAll.Add(TryValidateCredentials);
			return Validate(listAll);
		}

		///<summary>Logicoy has different credentials for each state it supports. Helper method that joins full state name and property description.</summary>
		public static string GetPropDesc(string state,string propDesc) {
			return state+" "+propDesc;
		}

		///<summary>Attempts to get full state name based on abbreviation. Throws if the state isn't one this bridge supports.</summary>
		private string GetStateFull() {
			if(!DictLogicoyStates.TryGetValue(StateAbbr??"",out string stateFull)) {
				throw new ApplicationException(Lans.g("PDMP","Unable to parse full name of state from state abbreviation."));
			}
			return stateFull;
		}

		#endregion

		#region Bamboo methods
		///<summary>Loads properties for Bamboo PDMP for the clinic, falling back to the practice rows.</summary>
		private void LoadBambooProps(long progNum,long clinicNum) {
			Username=GetVal(progNum,BambooUserName,clinicNum);
			Password=ReadSecret(progNum,BambooPassword,clinicNum,out IsPasswordReentryNeeded);
			Url=GetVal(progNum,BambooUrl,clinicNum);
			BambooClientKey=ReadSecret(progNum,BambooAuthToken,clinicNum,out bool isKeyLegacy);
			BambooClientPassword=ReadSecret(progNum,BambooAuthPassword,clinicNum,out bool isKeyPasswordLegacy);
			IsClientCertLegacy=isKeyLegacy || isKeyPasswordLegacy;
			if(IsClientCertLegacy) {
				//Open Dental's integrator certificate (or a damaged value). Never used, not even half of it.
				BambooClientKey="";
				BambooClientPassword="";
			}
			ClientCertThumbprint=PdmpCertificate.NormalizeThumbprint(GetVal(progNum,BambooClientCertThumbprint,clinicNum));
			FacilityId=GetVal(progNum,BambooFacilityID,clinicNum);
			LocationNpi=GetVal(progNum,BambooFacilityNpi,clinicNum);
			LocationDea=GetVal(progNum,BambooFacilityDea,clinicNum);
			SenderDeveloperOverride=GetVal(progNum,BambooSenderDeveloper,clinicNum);
			SenderProductOverride=GetVal(progNum,BambooSenderProduct,clinicNum);
		}

		///<summary>A client certificate must be set: a store thumbprint, or an imported file that opens, has a private key and hasn't expired.
		///Skipped in testing mode.</summary>
		private static bool TryValidateKey(PdmpProperty property,out string error) {
			error="";
			if(Introspection.IsTestingMode) {
				return true;
			}
			PdmpCertificateSource source=PdmpCertificate.GetSource(property);
			if(source==PdmpCertificateSource.None) {
				error=Lans.g("PDMP","Client certificate")+": "+PdmpCertificate.MessageNotSet+" "+property.GetSetupHint();
				return false;
			}
			if(source==PdmpCertificateSource.Database && string.IsNullOrEmpty(property.BambooClientPassword)) {
				return true;//TryValidatePassword reports this.
			}
			error=GetCertificateProblem(property,DateTime.UtcNow);
			return string.IsNullOrWhiteSpace(error);
		}

		///<summary>An imported certificate needs its password. A store certificate doesn't. Skipped in testing mode.</summary>
		private static bool TryValidatePassword(PdmpProperty property,out string error) {
			error="";
			if(Introspection.IsTestingMode) {
				return true;
			}
			if(PdmpCertificate.GetSource(property)==PdmpCertificateSource.Database && string.IsNullOrEmpty(property.BambooClientPassword)) {
				error=Lans.g("PDMP","Client certificate password was not set. Import the certificate again.")+" "+property.GetSetupHint();
			}
			return string.IsNullOrWhiteSpace(error);
		}

		///<summary>"" if the configured certificate is usable now, else why not. Loads the imported file in memory only (EphemeralKeySet).</summary>
		public static string GetCertificateProblem(PdmpProperty property,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			try {
				switch(PdmpCertificate.GetSource(property)) {
					case PdmpCertificateSource.Store:
						using(X509Certificate2 certificateStore=PdmpCertificate.FindInStore(property.ClientCertThumbprint)) {
							if(certificateStore==null) {
								return Lans.g("PDMP","The client certificate with the thumbprint in Setup is not in this computer's certificate store.");
							}
							return PdmpCertificate.GetProblem(certificateStore,dateTimeUtcNow);
						}
					case PdmpCertificateSource.Database:
						using(PdmpCertificate.Import(Convert.FromBase64String(property.BambooClientKey),property.BambooClientPassword,dateTimeUtcNow)) {
							return "";
						}
					default:
						return Lans.g("PDMP","Client certificate")+": "+PdmpCertificate.MessageNotSet;
				}
			}
			catch(PdmpException ex) {
				return ex.Message;
			}
			catch(FormatException ex) {
				ex.DoNothing();
				return Lans.g("PDMP","The client certificate in Setup could not be opened. Import it again.");
			}
		}

		private string ValidateBamboo() {
			List<PropertyValidator> listBambooRequirements=new List<PropertyValidator>() {
				TryValidateDea,TryValidateProvName,TryValidateUrl,TryValidateProvLicense,TryValidatePatBirthday,TryValidatePatZip,TryValidateState,
				TryValidateCredentials,TryValidateKey,TryValidatePassword
			};
			return Validate(listBambooRequirements);
		}

		#endregion region

		#region Setup window
		///<summary>Loads the Bamboo settings of one clinic (with fallback to the practice rows) for the setup window. No patient or provider.</summary>
		public static PdmpProperty LoadBambooForSetup(Program program,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			PdmpProperty pdmpProperty=new PdmpProperty() { ProgName=program.ProgName,ClinicNum=clinicNum };
			pdmpProperty.LoadBambooProps(program.ProgramNum,clinicNum);
			return pdmpProperty;
		}

		///<summary>The setup grid's "Client certificate" value: subject, issuer, expiry and thumbprint, or why there is none to use.</summary>
		public static string GetCertificateStatusText(PdmpProperty pdmpProperty,DateTime dateTimeUtcNow) {
			Meth.NoCheckMiddleTierRole();
			try {
				switch(PdmpCertificate.GetSource(pdmpProperty)) {
					case PdmpCertificateSource.Store:
						using(X509Certificate2 certificateStore=PdmpCertificate.FindInStore(pdmpProperty.ClientCertThumbprint)) {
							if(certificateStore==null) {
								return Lans.g("PDMP","Not found in this computer's certificate store:")+" "+pdmpProperty.ClientCertThumbprint;
							}
							return Lans.g("PDMP","Windows store")+": "+DescribeWithProblem(certificateStore,dateTimeUtcNow);
						}
					case PdmpCertificateSource.Database:
						//Ephemeral load: shows the details even of an expired certificate, without touching a key store.
						using(X509Certificate2 certificate=new X509Certificate2(Convert.FromBase64String(pdmpProperty.BambooClientKey),
							pdmpProperty.BambooClientPassword??"",X509KeyStorageFlags.EphemeralKeySet))
						{
							return DescribeWithProblem(certificate,dateTimeUtcNow);
						}
					default:
						return PdmpCertificate.MessageNotSet;
				}
			}
			catch(Exception ex) {
				ex.DoNothing();
				return Lans.g("PDMP","The client certificate in Setup could not be opened. Import it again.");
			}
		}

		private static string DescribeWithProblem(X509Certificate2 certificate,DateTime dateTimeUtcNow) {
			string problem=PdmpCertificate.GetProblem(certificate,dateTimeUtcNow);
			string warning=PdmpCertificate.IsExpiringSoon(certificate,dateTimeUtcNow) ? " ("+Lans.g("PDMP","expires soon")+")" : "";
			return PdmpCertificate.Describe(certificate)+warning+(problem=="" ? "" : " - "+problem);
		}
		#endregion Setup window

		#region Check setup
		///<summary>Checks a program's setup for one clinic without sending patient data: required rows, URL rules, the certificate (Bamboo), and that each
		///configured host answers a TLS handshake (a bodyless GET to the URL's origin; any HTTP status counts as reachable).</summary>
		public static List<PdmpSetupCheckItem> CheckSetup(Program program,long clinicNum,HttpMessageHandler httpMessageHandler=null) {
			Meth.NoCheckMiddleTierRole();
			List<PdmpSetupCheckItem> listItems=new List<PdmpSetupCheckItem>();
			EnsureProperties(program);
			if(program.ProgName==ProgramName.Bamboo.ToString()) {
				PdmpProperty pdmpProperty=new PdmpProperty() { ProgName=program.ProgName,ClinicNum=clinicNum };
				pdmpProperty.LoadBambooProps(program.ProgramNum,clinicNum);
				CheckOne(pdmpProperty,"",listItems,httpMessageHandler);
				return listItems;
			}
			if(program.ProgName==ProgramName.PDMP.ToString()) {
				foreach(KeyValuePair<string,string> state in DictLogicoyStates) {
					PdmpProperty pdmpProperty=new PdmpProperty() { ProgName=program.ProgName,ClinicNum=clinicNum,StateAbbr=state.Key };
					pdmpProperty.LoadPDMPProps(program.ProgramNum,clinicNum);
					if(string.IsNullOrWhiteSpace(pdmpProperty.Url) && string.IsNullOrWhiteSpace(pdmpProperty.Username)
						&& string.IsNullOrEmpty(pdmpProperty.Password) && !pdmpProperty.IsPasswordReentryNeeded)
					{
						continue;//This state isn't used.
					}
					CheckOne(pdmpProperty,state.Value+": ",listItems,httpMessageHandler);
				}
				if(listItems.Count==0) {
					listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,Lans.g("PDMP","No state is set up. Enter the url, username and password for your state.")));
				}
				return listItems;
			}
			listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,Lans.g("PDMP","Not a prescription monitoring program link.")));
			return listItems;
		}

		private static void CheckOne(PdmpProperty pdmpProperty,string prefix,List<PdmpSetupCheckItem> listItems,HttpMessageHandler httpMessageHandler) {
			bool isBamboo=pdmpProperty.ProgName==ProgramName.Bamboo.ToString();
			bool isUrlOk=TryValidateUrl(pdmpProperty,out string error);
			if(isUrlOk) {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Ok,prefix+Lans.g("PDMP","Url is set.")));
			}
			else {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+error));
			}
			//Check credentials even in testing mode: this is the setup check, not a debug call.
			if(pdmpProperty.IsPasswordReentryNeeded) {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+Lans.g("PDMP","The saved password can't be used by this program. Re-enter the password.")));
			}
			else if(string.IsNullOrWhiteSpace(pdmpProperty.Username) || string.IsNullOrEmpty(pdmpProperty.Password)) {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+Lans.g("PDMP","Username and password are required.")));
			}
			else {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Ok,prefix+Lans.g("PDMP","Username and password are set.")));
			}
			if(string.IsNullOrWhiteSpace(pdmpProperty.FacilityId)) {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Warning,prefix+Lans.g("PDMP","FacilityID is not set.")));
			}
			X509Certificate2 certificate=null;
			try {
				if(isBamboo) {
					string problem=GetCertificateProblem(pdmpProperty,DateTime.UtcNow);
					if(problem!="") {
						listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+problem));
					}
					else {
						certificate=PdmpCertificate.Load(pdmpProperty,DateTime.UtcNow);
						listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Ok,prefix+Lans.g("PDMP","Client certificate")+": "+PdmpCertificate.Describe(certificate)));
						string warning=PdmpCertificate.GetExpiryWarning(certificate,DateTime.UtcNow);
						if(warning!="") {
							listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Warning,prefix+warning));
						}
					}
				}
				if(!isUrlOk) {
					return;
				}
				string reachProblem=PdmpHttp.CheckReachable(pdmpProperty.Url,certificate,httpMessageHandler);
				if(reachProblem=="") {
					listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Ok,prefix+Lans.g("PDMP","The service answered.")));
				}
				else {
					listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+reachProblem));
				}
			}
			catch(PdmpException ex) {
				listItems.Add(new PdmpSetupCheckItem(PdmpSetupCheckLevel.Error,prefix+ex.Message));
			}
			finally {
				certificate?.Dispose();
			}
		}
		#endregion Check setup

		#region Debug Load
		///<summary>If we're in debug mode, this will grab the property values we need via introspection</summary>
		private void LoadDebugOverrides(Program progCur) {
			if(progCur.ProgName==ProgramName.PDMP.ToString()) {
				switch(StateAbbr) {
					case "IL":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserIL,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordIL,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdIL,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlIL,Url);
						break;
					case "CA":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserCA,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordCA,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdCA,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlCA,Url);
						break;
					case "WA":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserWA,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordWA,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdWA,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlWA,Url);
						break;
					case "MD":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserMD,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordMD,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdMD,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlMD,Url);
						break;
					case "UT":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserUT,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordUT,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdUT,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlUT,Url);
						break;
					case "KY":
						Username=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUserKY,Username);
						Password=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestPasswordKY,Password);
						FacilityId=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestFacilityIdKY,FacilityId);
						Url=Introspection.GetOverride(Introspection.IntrospectionEntity.PDMPTestUrlKY,Url);
						break;
				}
			}
			if(progCur.ProgName==ProgramName.Bamboo.ToString()) {
				Username=Introspection.GetOverride(Introspection.IntrospectionEntity.BambooTestUser,Username);
				Password=Introspection.GetOverride(Introspection.IntrospectionEntity.BambooTestPassword,Password);
				Url=Introspection.GetOverride(Introspection.IntrospectionEntity.BambooTestUrl,Url);
			}
		}
		#endregion
	}

	///<summary>How a setup check line reads.</summary>
	public enum PdmpSetupCheckLevel {
		Ok,
		Warning,
		Error,
	}

	///<summary>One line of a PDMP setup check.</summary>
	public class PdmpSetupCheckItem {
		public PdmpSetupCheckLevel Level { get; }
		public string Text { get; }

		public PdmpSetupCheckItem(PdmpSetupCheckLevel level,string text) {
			Level=level;
			Text=text;
		}

		public override string ToString() {
			string prefix;
			switch(Level) {
				case PdmpSetupCheckLevel.Error:
					prefix=Lans.g("PDMP","Problem")+": ";
					break;
				case PdmpSetupCheckLevel.Warning:
					prefix=Lans.g("PDMP","Warning")+": ";
					break;
				default:
					prefix=Lans.g("PDMP","OK")+": ";
					break;
			}
			return prefix+Text;
		}
	}
}
