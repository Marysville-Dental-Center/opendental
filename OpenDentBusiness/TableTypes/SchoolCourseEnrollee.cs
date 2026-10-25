using System;

namespace OpenDentBusiness {
	///<summary>A student enrolled in a dental-school course, with the computed course grade and an optional instructor override. Table added in Open
	///Dental 25.4. Dental-school features are excluded from this program; the table exists for schema compatibility.</summary>
	[Serializable]
	public class SchoolCourseEnrollee:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolCourseEnrolleeNum;
		///<summary>FK to schoolcourse.SchoolCourseNum.</summary>
		public long SchoolCourseNum;
		///<summary>FK to provider.ProvNum. The student.</summary>
		public long StudentNum;
		///<summary>The computed course grade. -1 when no evaluation counts yet.</summary>
		public float GradeNumber;
		///<summary>A grade the instructor entered to replace the computed one. -1 means no override.</summary>
		public float GradeOverride;

		public SchoolCourseEnrollee() {
			GradeNumber=-1;
			GradeOverride=-1;
		}

		public SchoolCourseEnrollee Copy() {
			return (SchoolCourseEnrollee)MemberwiseClone();
		}
	}
}
