using CodeBase;
using DataConnectionBase;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace OpenDentBusiness{
	///<summary></summary>
	public class OIDInternals{
		///<summary>The OID arc that the previous vendor registered for itself. Roots it issued to offices look like LegacyOidArc+".1486.&lt;n&gt;".
		///Kept only so that rows written under it can still be recognized (the DoseSpot patient-ID root in oidexternal is a local lookup key under
		///this prefix). Never mint new OIDs under it: an office's root is generated with GenerateUuidRoot or entered by the office.</summary>
		public const string LegacyOidArc="2.16.840.1.113883.3.4337";
		///<summary>The ITU-T X.667 | ISO/IEC 9834-8 arc for UUID-based OIDs, which need no registration.</summary>
		public const string UuidArc="2.25";
		///<summary>The longest root the OID registry accepts. Child OIDs add "."+(int)IdentifierType to it.</summary>
		public const int MaxRootLength=64;
		private static readonly Regex _regexOid=new Regex(@"^[0-2](\.(0|[1-9][0-9]*))+$",RegexOptions.CultureInvariant);

		///<summary>Old name of LegacyOidArc, kept until its remaining callers move.</summary>
		[Obsolete("Use LegacyOidArc. The arc belongs to the previous vendor and is only matched for legacy rows.")]
		public static string OpenDentalOID {
			get {
				return LegacyOidArc;
			}
		}
		private static long _customerPatNum=0;

		///<summary>The PatNum at Open Dental HQ associated to this database's registration key.
		///Makes a web call to WebServiceCustomerUpdates in order to get the PatNum from HQ.
		///Throws exceptions to show to the user if anything goes wrong in communicating with the web service.  Exceptions are already translated.
		///This build has no HQ, so it always throws. The OID registry no longer uses it; it is deleted once the DoseSpot root stops calling it.</summary>
		public static long CustomerPatNum {
			get {
				if(_customerPatNum==0) {
					//prepare the xml document to send--------------------------------------------------------------------------------------
					XmlWriterSettings settings=new XmlWriterSettings();
					settings.Indent=true;
					settings.IndentChars=("    ");
					StringBuilder strbuild=new StringBuilder();
					using(XmlWriter writer = XmlWriter.Create(strbuild,settings)) {
						writer.WriteStartElement("CustomerIdRequest");
						writer.WriteStartElement("RegistrationKey");
						writer.WriteString(PrefC.GetString(PrefName.RegistrationKey));
						writer.WriteEndElement();
						writer.WriteStartElement("RegKeyDisabledOverride");
						writer.WriteString("true");
						writer.WriteEndElement();
						writer.WriteEndElement();
					}
					ICustomerUpdates OIDService=CustomerUpdatesProxy.GetWebServiceInstance();//Not available in this build. Throws HqNotAvailableException.
					//Send the message and get the result---------------------------------------------------------------------------------------
					string result="";
					try {
						result=OIDService.RequestCustomerID(strbuild.ToString());
					}
					catch(Exception ex) {
						throw new Exception(Lans.g("OIDInternals","Error obtaining CustomerID:")+" "+ex.Message);
					}
					XmlDocument doc=new XmlDocument();
					doc.LoadXml(result);
					//Process errors------------------------------------------------------------------------------------------------------------
					XmlNode node=doc.SelectSingleNode("//Error");
					if(node!=null) {
						throw new Exception(Lans.g("OIDInternals","Error:")+" "+node.InnerText);
					}
					//Process a valid return value----------------------------------------------------------------------------------------------
					node=doc.SelectSingleNode("//CustomerIdResponse");
					if(node==null) {
						throw new ODException(Lans.g("OIDInternals","There was an error requesting your OID or processing the result of the request.  Please try again."));
					}
					if(node.InnerText=="") {
						throw new ODException(Lans.g("OIDInternals","Invalid registration key.  Your OIDs will have to be set manually."));
					}
					//CustomerIdResponse has been returned and is not blank
					_customerPatNum=PIn.Long(node.InnerText);
				}
				return _customerPatNum;
			}
		}

		///<summary>Returns a new root under the UUID arc: "2.25." followed by the decimal value of a random (version 4) UUID, per ITU-T X.667.
		///That arc needs no registration. The 122 random bits come from a cryptographic RNG, as the ITU recommends, so collisions are not a practical
		///concern. The result matches ^2\.25\.[1-9][0-9]{0,38}$ and is at most 44 characters long. Works offline; nothing is sent anywhere.</summary>
		public static string GenerateUuidRoot() {
			byte[] arrayBytes=new byte[16];
			using(RNGCryptoServiceProvider rng=new RNGCryptoServiceProvider()) {
				rng.GetBytes(arrayBytes);
			}
			return UuidArc+"."+UuidBytesToDecimal(arrayBytes);
		}

		///<summary>Sets the version 4 and RFC 4122 variant bits on 16 bytes in network (big-endian) order and returns the UUID's value as an
		///unsigned decimal integer without leading zeros. The input array is not changed.</summary>
		public static string UuidBytesToDecimal(byte[] arrayBytes) {
			if(arrayBytes==null || arrayBytes.Length!=16) {
				throw new ArgumentException("A UUID has 16 bytes.",nameof(arrayBytes));
			}
			byte[] arrayValue=(byte[])arrayBytes.Clone();
			arrayValue[6]=(byte)((arrayValue[6] & 0x0F) | 0x40);//version 4 (random)
			arrayValue[8]=(byte)((arrayValue[8] & 0x3F) | 0x80);//variant 10xx (RFC 4122)
			//Repeated division of the big-endian base-256 number by 10. The version bits guarantee the value is not zero.
			StringBuilder stringBuilderDigits=new StringBuilder();
			bool isZero=false;
			while(!isZero) {
				int remainder=0;
				isZero=true;
				for(int i=0;i<arrayValue.Length;i++) {
					int current=(remainder<<8)+arrayValue[i];
					arrayValue[i]=(byte)(current/10);
					remainder=current%10;
					if(arrayValue[i]!=0) {
						isZero=false;
					}
				}
				stringBuilderDigits.Insert(0,(char)('0'+remainder));
			}
			return stringBuilderDigits.ToString();
		}

		///<summary>True if the string is a well-formed OID in dotted decimal notation: a first arc of 0, 1 or 2, at least one more arc, no leading
		///zeros, no blank arcs and no spaces. Also enforces MaxRootLength, since this is what roots are checked with.</summary>
		public static bool IsValidOid(string oid) {
			if(string.IsNullOrEmpty(oid) || oid.Length>MaxRootLength) {
				return false;
			}
			return _regexOid.IsMatch(oid);
		}

		///<summary>True if the root was issued from the previous vendor's arc. Changing such a root changes identifiers that HL7 and CCD trading
		///partners may already hold, so the office decides; it is never replaced automatically.</summary>
		public static bool IsLegacyRoot(string root) {
			return !string.IsNullOrEmpty(root) && root.StartsWith(LegacyOidArc+".",StringComparison.Ordinal);
		}

		///<summary>The value a row gets under the given root: the root itself for the Root row, otherwise root+"."+(int)IDType, the numbering used
		///all along (LabOrder .1, Patient .2 ... Procedure .8). Blank if the root is blank.</summary>
		public static string GetRecommendedValue(string root,IdentifierType idType) {
			if(string.IsNullOrEmpty(root)) {
				return "";
			}
			if(idType==IdentifierType.Root) {
				return root;
			}
			return root+"."+((int)idType).ToString();
		}

		///<summary>True if any child row holds a value set by hand: not blank and not the recommended value under the list's current root.
		///Used to ask before ApplyRootToList overwrites them.</summary>
		public static bool HasCustomChildren(List<OIDInternal> listOIDInternals) {
			string rootCur=GetRootFromList(listOIDInternals);
			return listOIDInternals.Any(x => IsCustomChild(x,rootCur));
		}

		///<summary>Sets the Root row of the list to the new root and the child rows to root.N. Child rows that are blank, or that still hold the
		///recommended value under the old root, always follow the root. Custom child values are replaced only when overwriteChildren is true.
		///Changes only the objects in the list; the caller saves them. Throws if the root is not a valid OID.</summary>
		public static void ApplyRootToList(List<OIDInternal> listOIDInternals,string root,bool overwriteChildren) {
			if(!IsValidOid(root)) {
				throw new ODException(Lans.g("OIDInternals","The root is not a valid OID:")+" "+root);
			}
			string rootOld=GetRootFromList(listOIDInternals);
			for(int i=0;i<listOIDInternals.Count;i++) {
				if(listOIDInternals[i].IDType!=IdentifierType.Root && !overwriteChildren && IsCustomChild(listOIDInternals[i],rootOld)) {
					continue;
				}
				listOIDInternals[i].IDRoot=GetRecommendedValue(root,listOIDInternals[i].IDType);
			}
		}

		///<summary>Sets the office's root in the database and fills the child rows the way ApplyRootToList does. Inserts any missing rows first.
		///Throws if the root is not a valid OID.</summary>
		public static void ApplyRoot(string root,bool overwriteChildren) {
			//No remoting role check; GetAll and Update do their own.
			List<OIDInternal> listOIDInternals=GetAll();
			ApplyRootToList(listOIDInternals,root,overwriteChildren);
			for(int i=0;i<listOIDInternals.Count;i++) {
				Update(listOIDInternals[i]);
			}
		}

		private static string GetRootFromList(List<OIDInternal> listOIDInternals) {
			OIDInternal oIDInternalRoot=listOIDInternals.FirstOrDefault(x => x.IDType==IdentifierType.Root);
			if(oIDInternalRoot==null || oIDInternalRoot.IDRoot==null) {
				return "";
			}
			return oIDInternalRoot.IDRoot;
		}

		private static bool IsCustomChild(OIDInternal oIDInternal,string rootCur) {
			if(oIDInternal.IDType==IdentifierType.Root || string.IsNullOrEmpty(oIDInternal.IDRoot)) {
				return false;
			}
			return oIDInternal.IDRoot!=GetRecommendedValue(rootCur,oIDInternal.IDType);
		}

		///<summary>Returns the currently defined OID for a given IndentifierType.  If not defined, IDroot will be empty string.</summary>
		public static OIDInternal GetForType(IdentifierType IDType) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<OIDInternal>(MethodBase.GetCurrentMethod(),IDType);
			}
			InsertMissingValues();//
			string command="SELECT * FROM oidinternal WHERE IDType='"+IDType.ToString()+"'";//should only return one row.
			return Crud.OIDInternalCrud.SelectOne(command);
		}

		///<summary>There should always be one entry in the DB per IdentifierType enumeration.</summary>
		public static void InsertMissingValues() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod());
				return;
			}
			//string command= "SELECT COUNT(*) FROM oidinternal";
			//if(PIn.Long(Db.GetCount(command))==Enum.GetValues(typeof(IdentifierType)).Length) {
			//	return;//The DB table has the right count. Which means there is probably nothing wrong with the values in it. This may need to be enhanced if customers have any issues.
			//}
			string command="SELECT * FROM oidinternal";
			List<OIDInternal> listOIDInternals=Crud.OIDInternalCrud.SelectMany(command);
			List<IdentifierType> listIDTypes=new List<IdentifierType>();
			for(int i=0;i<listOIDInternals.Count;i++) {
				listIDTypes.Add(listOIDInternals[i].IDType);
			}
			for(int i=0;i<Enum.GetValues(typeof(IdentifierType)).Length;i++) {
				if(listIDTypes.Contains((IdentifierType)i)) {
					continue;//DB contains a row for this enum value.
				}
				//Insert missing row with blank OID.
				if(DataConnection.DBtype==DatabaseType.MySql) {
						command="INSERT INTO oidinternal (IDType,IDRoot) "
						+"VALUES('"+((IdentifierType)i).ToString()+"','')";
						Db.NonQ32(command);
				}
				else {//oracle
					command="INSERT INTO oidinternal (OIDInternalNum,IDType,IDRoot) "
						+"VALUES((SELECT MAX(OIDInternalNum)+1 FROM oidinternal),'"+((IdentifierType)i).ToString()+"','')";
					Db.NonQ32(command);
				}
			}
		}

		///<summary></summary>
		public static List<OIDInternal> GetAll() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<OIDInternal>>(MethodBase.GetCurrentMethod());
			}
			InsertMissingValues();//there should always be one entry in the DB for each IdentifierType enumeration, insert any missing
			string command="SELECT * FROM oidinternal";
			return Crud.OIDInternalCrud.SelectMany(command);
		}

		///<summary></summary>
		public static void Update(OIDInternal oIDInternal) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),oIDInternal);
				return;
			}
			Crud.OIDInternalCrud.Update(oIDInternal);
		}

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.

		///<summary></summary>
		public static List<OIDInternal> Refresh(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<OIDInternal>>(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM oidinternal WHERE PatNum = "+POut.Long(patNum);
			return Crud.OIDInternalCrud.SelectMany(command);
		}

		///<summary>Gets one OIDInternal from the db.</summary>
		public static OIDInternal GetOne(long ehrOIDNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<OIDInternal>(MethodBase.GetCurrentMethod(),ehrOIDNum);
			}
			return Crud.OIDInternalCrud.SelectOne(ehrOIDNum);
		}

		///<summary></summary>
		public static long Insert(OIDInternal oIDInternal){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				oIDInternal.EhrOIDNum=Meth.GetLong(MethodBase.GetCurrentMethod(),oIDInternal);
				return oIDInternal.EhrOIDNum;
			}
			return Crud.OIDInternalCrud.Insert(oIDInternal);
		}

		///<summary></summary>
		public static void Delete(long ehrOIDNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),ehrOIDNum);
				return;
			}
			string command= "DELETE FROM oidinternal WHERE EhrOIDNum = "+POut.Long(ehrOIDNum);
			Db.NonQ(command);
		}
		*/
	}
}