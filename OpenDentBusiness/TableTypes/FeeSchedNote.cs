using System;

namespace OpenDentBusiness {
	///<summary>A free-text note attached to a fee schedule, optionally limited to some clinics. Table added in Open Dental 24.4. The fee schedule note
	///feature package builds the UI; this class and its CRUD exist so the table's data loads.</summary>
	[Serializable]
	public class FeeSchedNote:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long FeeSchedNoteNum;
		///<summary>FK to feesched.FeeSchedNum.</summary>
		public long FeeSchedNum;
		///<summary>Comma-separated list of clinic.ClinicNum values the note applies to. Empty when it applies to every clinic.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string ClinicNums;
		///<summary>The note.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText)]
		public string Note;
		///<summary>The date shown with the note. Editable by the user.</summary>
		public DateTime DateEntry;
		///<summary>FK to userod.UserNum. The user who added the note.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.ExcludeFromUpdate)]
		public long SecUserNumEntry;
		///<summary>Date the row was inserted. Set by the database layer, not editable.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateEntry)]
		public DateTime SecDateEntry;
		///<summary>Automatically updated by MySQL every time a row is added or changed.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.TimeStamp)]
		public DateTime SecDateTEdit;

		public FeeSchedNote() {
			ClinicNums="";
			Note="";
		}

		public FeeSchedNote Copy() {
			return (FeeSchedNote)MemberwiseClone();
		}
	}
}
