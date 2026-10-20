using System;

namespace OpenDentBusiness {
	///<summary>A weekly time slot for a dental-school course definition or course, or a dated override or cancellation. Table added in Open Dental
	///25.3. Dental-school features are excluded from this program; the table exists for schema compatibility.</summary>
	[Serializable]
	public class SchoolCourseSched:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long SchoolCourseSchedNum;
		///<summary>FK to schoolcoursedef.SchoolCourseDefNum.</summary>
		public long SchoolCourseDefNum;
		///<summary>FK to schoolcourse.SchoolCourseNum.</summary>
		public long SchoolCourseNum;
		///<summary>Start time of the slot.</summary>
		public TimeSpan TimeStart;
		///<summary>End time of the slot.</summary>
		public TimeSpan TimeEnd;
		///<summary>0 is Sunday through 6 for Saturday. Ignored for overrides.</summary>
		public DayOfWeek DayOfTheWeek;
		///<summary>The date an override applies to.</summary>
		public DateTime DateOverride;
		///<summary>True for a dated override row.</summary>
		public bool IsOverride;
		///<summary>True when the slot is canceled.</summary>
		public bool IsCanceled;

		public SchoolCourseSched Copy() {
			return (SchoolCourseSched)MemberwiseClone();
		}
	}
}
