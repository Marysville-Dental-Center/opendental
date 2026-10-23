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
		///<summary>The value for this type.</summary>
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

	///<summary>Stored in branding.BrandingType. Numbers as published in the Open Dental 25.3 database documentation. 26.1 renames 1 to LogoImage.</summary>
	public enum EnumBrandingType {
		///<summary>0</summary>
		None=0,
		///<summary>1</summary>
		LogoFilePath=1,
		///<summary>2</summary>
		MaterialColorPalette=2,
		///<summary>3</summary>
		OfficeDescription=3,
	}
}
