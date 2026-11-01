using System;

namespace OpenPmsDentalBusiness {
	///<summary>A reusable dental-school course definition from which yearly schoolcourse rows are scheduled. Table added in Open Dental 25.3.
	///Dental-school features are excluded from this program; the table exists for schema compatibility.</summary>
	[Serializable]
	public class SchoolCourseDef:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolCourseDefNum;
		///<summary>Alphanumeric course ID.</summary>
		public string CourseID;
		///<summary>Course title.</summary>
		public string Descript;
		///<summary>Added in Open Dental 25.4. FK to gradingscale.GradingScaleNum: the grading scale of courses made from this definition.</summary>
		public long GradingScaleNum;

		public SchoolCourseDef() {
			CourseID="";
			Descript="";
		}

		public SchoolCourseDef Copy() {
			return (SchoolCourseDef)MemberwiseClone();
		}
	}
}
