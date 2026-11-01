using System;

namespace OpenDentBusiness {
	///<summary>A serialized copy of a deleted row with the time it was deleted. Table added in Open Dental 26.1, where its writers feed the local REST
	///API's change subscriptions. That API is not part of this program, so nothing here writes or reads the table; it exists for schema and data
	///compatibility. Not related to the older deletedobject table (DeletedObject), which stays.</summary>
	[Serializable]
	public class DeletedObj:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long DeletedObjNum;
		///<summary>When the row was added, in server time.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateTEntry)]
		public DateTime DateTEntry;
		///<summary>Enum:EnumDeletedObjType The kind of row that was deleted.</summary>
		public EnumDeletedObjType ObjType;
		///<summary>The deleted row, serialized. mediumtext.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ObjSerialized;

		public DeletedObj() {
			ObjSerialized="";
		}

		public DeletedObj Copy() {
			return (DeletedObj)MemberwiseClone();
		}
	}

	///<summary>Stored in deletedobj.ObjType. Numbers as published in the Open Dental 26.1 database documentation.</summary>
	public enum EnumDeletedObjType {
		///<summary>0 - Not used.</summary>
		None=0,
		///<summary>1 - Schedules, blockouts included.</summary>
		Schedules=1,
		///<summary>2</summary>
		LabCases=2,
		///<summary>3</summary>
		MedicationPats=3,
		///<summary>4</summary>
		PatFields=4,
		///<summary>5</summary>
		ToothInitials=5,
	}
}
