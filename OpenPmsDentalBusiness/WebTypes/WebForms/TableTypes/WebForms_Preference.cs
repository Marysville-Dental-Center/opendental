using OpenPmsDentalBusiness;
using System.Drawing;
using System.Xml.Serialization;
using System;
using System.Runtime.Serialization;

namespace OpenPmsDentalBusiness.WebTypes.WebForms {
	[Serializable]
	[CrudTable(CrudLocationOverride=@"..\..\..\OpenPmsDentalBusiness\WebTypes\WebForms\Crud",NamespaceOverride="OpenPmsDentalBusiness.WebTypes.WebForms.Crud",CrudExcludePrefC=true)]
	public class WebForms_Preference:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long WebFormPrefNum;
		///<summary></summary>
		[XmlIgnore]
		public Color ColorBorder;
		///<summary></summary>
		public string CultureName;
		///<summary>When true disables signatures in web forms.  False by default.</summary>
		public bool DisableSignatures;
		///<summary>FK to customers.registrationkey.RegistrationKeyNum</summary>
		public long RegistrationKeyNum;
		///<summary>FK to customers.patient.PatNum.</summary>
		public long DentalOfficeID;
		///<summary>True causes webforms_log entries to be made for this customer regardless of the log level on the servers at HQ.</summary>
		public bool IsLogging;

		///<summary>Used only for serialization purposes</summary>
		[XmlElement("ColorBorder",typeof(int))]
		public int ColorOverrideXml {
			get {
				return ColorBorder.ToArgb();
			}
			set {
				ColorBorder=Color.FromArgb(value);
			}
		}

    public WebForms_Preference(){
			
		}

    public WebForms_Preference(string cultureName=""){
			this.ColorBorder=Color.White;
			this.CultureName=cultureName;
		}
		
    public WebForms_Preference(Color colorBorder, string cultureName=""){
			this.ColorBorder=colorBorder;
			this.CultureName=cultureName;	
		}
		
    public WebForms_Preference Copy(){
			return (WebForms_Preference)this.MemberwiseClone();
		}
	}
}