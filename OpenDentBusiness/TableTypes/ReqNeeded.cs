using System;
using System.Collections.Generic;
using System.Text;

namespace OpenDentBusiness{
	///<summary>For Dental Schools.  Requirements needed in order to complete a course.</summary>
	[Serializable]
	[CrudTable(IsSynchable=true)]
	public class ReqNeeded:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long ReqNeededNum;
		///<summary>.</summary>
		public string Descript;
		///<summary>FK to schoolcourse.SchoolCourseNum.  Never 0.</summary>
		public long SchoolCourseNum;
		///<summary>FK to schoolclass.SchoolClassNum.  Never 0.</summary>
		public long SchoolClassNum;
		///<summary>FK to schoolcoursedef.SchoolCourseDefNum. Added in Open Dental 25.3: ties the requirement to a course definition. 0 for course-level
		///or graduation requirements. Kept for data compatibility; dental-school features are not part of this program.</summary>
		public long SchoolCourseDefNum;

		///<summary></summary>
		public ReqNeeded Copy(){
			ReqNeeded r=new ReqNeeded();
			r.ReqNeededNum=ReqNeededNum;
			r.Descript=Descript;
			r.SchoolCourseNum=SchoolCourseNum;
			r.SchoolClassNum=SchoolClassNum;
			r.SchoolCourseDefNum=SchoolCourseDefNum;
			return r;
		}
	}


}
