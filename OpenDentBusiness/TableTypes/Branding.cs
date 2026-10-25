using System;

namespace OpenDentBusiness {
	///<summary>Per-clinic branding values for Open Dental's patient-facing hosted apps. Table added in Open Dental 25.2. Kept only for schema and data
	///compatibility: those apps are not part of this program, and nothing here reads or writes the table.</summary>
	[Serializable]
	public class Branding:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long BrandingNum;
		///<summary>Enum:EnumBrandingType</summary>
		public EnumBrandingType BrandingType;
		///<summary>FK to clinic.ClinicNum. 0 is the practice default.</summary>
		public long ClinicNum;
		///<summary>The value for this type. mediumtext since Open Dental 26.1, so a logo image fits as base64.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ValueString;
		///<summary>When the value last changed.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeUpdated;

		public Branding() {
			ValueString="";
		}

		public Branding Copy() {
			return (Branding)MemberwiseClone();
		}
	}

	///<summary>Stored in branding.BrandingType. Numbers and names as published in the Open Dental 26.1 database documentation. Value 1 was LogoFilePath
	///in the 25.2 and 25.3 documentation; 26.1 calls it LogoImage and stores the logo itself (base64) instead of a file path. Either way, nothing in
	///this program reads it.</summary>
	public enum EnumBrandingType {
		///<summary>0</summary>
		None=0,
		///<summary>1 - The logo image, base64 (26.1). Rows written before 26.1 may hold a file path.</summary>
		LogoImage=1,
		///<summary>2</summary>
		MaterialColorPalette=2,
		///<summary>3</summary>
		OfficeDescription=3,
	}
}
