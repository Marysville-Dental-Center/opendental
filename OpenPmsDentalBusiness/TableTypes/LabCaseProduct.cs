using System;

namespace OpenPmsDentalBusiness {
	///<summary>One product of a lab case (for example a crown or a night guard) with its material, shade and teeth. Table added in Open Dental 26.2
	///(OpenDentalDiffDocumentation26-2.xml); names, order and types as published. This class gives the schema and the stored data a home; sending
	///lab cases to Glidewell is a separate feature package.</summary>
	[Serializable]
	public class LabCaseProduct:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long LabCaseProductNum;
		///<summary>FK to labcase.LabCaseNum.</summary>
		public long LabCaseNum;
		///<summary>FK to patient.PatNum.</summary>
		public long PatNum;
		///<summary>Enum:EnumGlidewellProductName The product, from a list Glidewell supplies (for example crown, flipper, night guard).</summary>
		public EnumGlidewellProductName GlidewellProduct;
		///<summary>The material the product is made of, for example "BruxZir".</summary>
		public string Material;
		///<summary>The shade of the material, for example "A1".</summary>
		public string Shade;
		///<summary>The teeth the product is for, from procedurelog.ToothRange, formatted for display.</summary>
		public string ToothRange;
		///<summary>Notes about the product for the lab.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Notes;

		public LabCaseProduct() {
			Material="";
			Shade="";
			ToothRange="";
			Notes="";
		}

		///<summary>Returns a copy of this LabCaseProduct.</summary>
		public LabCaseProduct Copy() {
			return (LabCaseProduct)MemberwiseClone();
		}
	}

	///<summary>Stored in labcaseproduct.GlidewellProduct (tinyint). Open Dental's 26.2 database documentation names this enumeration ("From a list
	///supplied by Glidewell") but publishes none of its values, so none are defined here: a stored number loads as it is and is written back
	///unchanged. Values may only be added with Open Dental's published numbers.</summary>
	public enum EnumGlidewellProductName {
	}
}
