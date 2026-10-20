using System;

namespace OpenDentBusiness {
	///<summary>An instructor's sign-off on one item of a dental student's work. Table added in Open Dental 25.3 (schema only; the workflow came in 25.4).
	///Dental-school features are excluded from this program (owner decision 2026-10-18); the table exists for schema compatibility. Each row has
	///exactly one non-zero FK among AptNum, ProcNum, TreatPlanNum, PerioExamNum, AllergyNum, DiseaseNum, DocNum and MountNum.</summary>
	[Serializable]
	public class SchoolApproval:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolApprovalNum;
		///<summary>FK to provider.ProvNum. The student. Never 0.</summary>
		public long ProvNum;
		///<summary>The review status of the item. Open Dental does not publish the values.</summary>
		public byte SignOffStatus;
		///<summary>FK to provider.ProvNum. The instructor.</summary>
		public long InstructorNum;
		///<summary>FK to appointment.AptNum.</summary>
		public long AptNum;
		///<summary>FK to procedurelog.ProcNum.</summary>
		public long ProcNum;
		///<summary>FK to treatplan.TreatPlanNum.</summary>
		public long TreatPlanNum;
		///<summary>FK to perioexam.PerioExamNum.</summary>
		public long PerioExamNum;
		///<summary>FK to allergy.AllergyNum.</summary>
		public long AllergyNum;
		///<summary>FK to disease.DiseaseNum.</summary>
		public long DiseaseNum;
		///<summary>FK to document.DocNum.</summary>
		public long DocNum;
		///<summary>FK to mount.MountNum.</summary>
		public long MountNum;
		///<summary>Date the row was inserted. Set by the database layer, not editable.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateEntry)]
		public DateTime SecDateEntry;
		///<summary>Automatically updated by MySQL every time a row is added or changed.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.TimeStamp)]
		public DateTime SecDateTEdit;

		public SchoolApproval Copy() {
			return (SchoolApproval)MemberwiseClone();
		}
	}
}
