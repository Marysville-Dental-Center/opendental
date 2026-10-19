using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenDentBusiness;

namespace UnitTestsCore {
	public class RepeatChargeT {
		public static RepeatCharge CreateRepeatCharge(long patNum,string procCode,double chargeAmt,DateTime dateStart=default) {
			RepeatCharge repeatCharge=new RepeatCharge {
				ChargeAmt=chargeAmt,
				ChargeAmtAlt=-1,
				DateStart=dateStart==default ? DateTime.Today : dateStart,
				IsEnabled=true,
				PatNum=patNum,
				ProcCode=procCode,
			};
			Patient oldPat=Patients.GetPat(repeatCharge.PatNum);
			repeatCharge.RepeatChargeNum=RepeatCharges.Insert(repeatCharge);
			RepeatCharges.InsertRepeatChargeChangeSecurityLogEntry(repeatCharge,EnumPermType.RepeatChargeCreate,oldPat,isAutomated:true);
			return repeatCharge;
		}
	}
}
