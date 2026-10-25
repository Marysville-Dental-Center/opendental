using System;

namespace OpenDentBusiness {
	///<summary>An instructor assigned to a dental-school course. Table added in Open Dental 25.4. Dental-school features are excluded from this
	///program; the table exists for schema compatibility.</summary>
	[Serializable]
	public class SchoolCourseInstructor:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolCourseInstructorNum;
		///<summary>FK to schoolcourse.SchoolCourseNum.</summary>
		public long SchoolCourseNum;
		///<summary>FK to provider.ProvNum. The instructor.</summary>
		public long InstructorNum;

		public SchoolCourseInstructor Copy() {
			return (SchoolCourseInstructor)MemberwiseClone();
		}
	}
}
