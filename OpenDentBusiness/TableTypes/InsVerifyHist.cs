using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenDentBusiness {
	///<summary>A historical copy of an insurance verification record.</summary>
	[Serializable]
	public class InsVerifyHist:TableBase {
		#region Not copied from InsVerify
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long InsVerifyHistNum;
		///<summary>FK to userod.UserNum.  User that was logged on when row was inserted.</summary>
		public long VerifyUserNum;
		#endregion Not copied from InsVerify

		#region Copies of InsVerify Fields
		///<summary>Copied from InsVerify.</summary>
		public long InsVerifyNum;
		///<summary>Copied from InsVerify.</summary>
		public DateTime DateLastVerified;
		///<summary>Copied from InsVerify.</summary>
		public long UserNum;
		///<summary>Copied from InsVerify.</summary>
		public VerifyTypes VerifyType;
		///<summary>Copied from InsVerify.</summary>
		public long FKey;
		///<summary>Copied from InsVerify.</summary>
		public long DefNum;
		///<summary>Copied from InsVerify.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.IsText | CrudSpecialColType.CleanText)]
		public string Note;
		///<summary>Copied from InsVerify.</summary>
		public DateTime DateLastAssigned;
		///<summary>Copied from InsVerify.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.DateT)]
		public DateTime DateTimeEntry;
		///<summary>Copied from InsVerify.</summary>
		public double HoursAvailableForVerification;
		///<summary>Not copied from Task. Automatically updated by MySQL every time a row is added or changed.</summary>
		[CrudColumn(SpecialType=CrudSpecialColType.TimeStamp)]
		public DateTime SecDateTEdit;
		///<summary>Added in Open Dental 25.1 (inssub in 25.2): row-integrity hash over the protected fields, to notice edits made outside the program.
		///Open Dental's values cannot be checked by this program; the row-integrity package rehashes them with this program's own hash.</summary>
		public string SecurityHash;
		#endregion Copies of InsVerify Fields

		#region Not Db Columns
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long PatNum;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long PlanNum;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long PatPlanNum;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public string ClinicName;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public string PatientName;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public string CarrierName;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public DateTime AppointmentDateTime;
		///<summary>Not a database column.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long AptNum;
		///<summary>Not a database column.  This ClinicNum comes from the appointment.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long ClinicNum;
		///<summary>Not a database column.  The InsSubNum associated to this PatPlanNum's patPlan.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long InsSubNum;
		///<summary>Not a database column.  The CarrierNum associated to this CarrierName's carrierNum.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public long CarrierNum;
		///<summary>Not a database column.  Used by UnitTest to verify batch ins verify service.</summary>
		[CrudColumn(IsNotDbColumn=true)]
		public BatchInsVerifyState BatchVerifyState;
		#endregion Not Db Columns

		public InsVerifyHist() {
		}

		public InsVerifyHist(InsVerify insVerify) {
			VerifyUserNum=Security.CurUser.UserNum;
			InsVerifyNum=insVerify.InsVerifyNum;
			DateLastVerified=insVerify.DateLastVerified;
			UserNum=insVerify.UserNum;
			VerifyType=insVerify.VerifyType;
			FKey=insVerify.FKey;
			DefNum=insVerify.DefNum;
			Note=insVerify.Note;
			DateLastAssigned=insVerify.DateLastAssigned;
			DateTimeEntry=insVerify.DateTimeEntry;
			HoursAvailableForVerification=insVerify.HoursAvailableForVerification;
			SecDateTEdit=insVerify.SecDateTEdit;
			SecurityHash=insVerify.SecurityHash;//The hashed fields are copied unchanged, so the copied hash stays valid.
		}

		///<summary></summary>
		public InsVerifyHist Copy() {
			return (InsVerifyHist)this.MemberwiseClone();
		}
	}
}
