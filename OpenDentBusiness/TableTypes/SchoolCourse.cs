using System;
using System.Collections;

namespace OpenDentBusiness{

	///<summary>Used in dental schools.  eg OP 732 Operative Dentistry Clinic II.</summary>
	[Serializable]
	public class SchoolCourse:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolCourseNum;
		///<summary>Alphanumeric.  eg PEDO 732.</summary>
		public string CourseID;
		///<summary>eg: Pediatric Dentistry Clinic II</summary>
		public string Descript;
		///<summary>Added in Open Dental 25.3: first day of the course's default schedule. Kept for data compatibility; dental-school features are not
		///part of this program.</summary>
		public DateTime DateStart;
		///<summary>Added in Open Dental 25.3: last day of the course's default schedule.</summary>
		public DateTime DateEnd;
		///<summary>FK to schoolclass.SchoolClassNum. Added in Open Dental 25.3: the class cohort that takes this course. 0 for older courses.</summary>
		public long SchoolClassNum;
		///<summary>FK to gradingscale.GradingScaleNum. Added in Open Dental 25.3: the grading scale every evaluation of the course uses.</summary>
		public long GradingScaleNum;

		///<summary></summary>
		public SchoolCourse Copy(){
			SchoolCourse sc=new SchoolCourse();
			sc.SchoolCourseNum=SchoolCourseNum;
			sc.CourseID=CourseID;
			sc.Descript=Descript;
			sc.DateStart=DateStart;
			sc.DateEnd=DateEnd;
			sc.SchoolClassNum=SchoolClassNum;
			sc.GradingScaleNum=GradingScaleNum;
			return sc;
		}

	}

	

	


}




















