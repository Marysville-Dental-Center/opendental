using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CodeBase;
using ODCrypt;

namespace OpenDentBusiness{
	///<summary></summary>
	public class UserWebs {
		#region Get Methods

		///<summary>Returns true if the patient has portal access.</summary>
		public static bool HasPatientPortalAccess(long patNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),patNum);
			}
			string command="SELECT * FROM userweb WHERE FKeyType="+POut.Int((int)UserWebFKeyType.PatientPortal)+" "
				+"AND FKey="+POut.Long(patNum);
			UserWeb userWeb=Crud.UserWebCrud.SelectOne(command);
			if(userWeb!=null && userWeb.PasswordHash!="") {
				return true;
			}
			return false;
		}

		///<summary>Gets one UserWeb from the db.</summary>
		public static UserWeb GetOne(long userWebNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<UserWeb>(MethodBase.GetCurrentMethod(),userWebNum);
			}
			return Crud.UserWebCrud.SelectOne(userWebNum);
		}

		#endregion

		#region Insert

		///<summary></summary>
		public static long Insert(UserWeb userWeb) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				userWeb.UserWebNum=Meth.GetLong(MethodBase.GetCurrentMethod(),userWeb);
				return userWeb.UserWebNum;
			}
			return Crud.UserWebCrud.Insert(userWeb);
		}

		#endregion

		#region Update

		///<summary></summary>
		public static void Update(UserWeb userWeb) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),userWeb);
				return;
			}
			Crud.UserWebCrud.Update(userWeb);
		}

		///<summary></summary>
		public static void Update(UserWeb userWeb,UserWeb userWebOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),userWeb,userWebOld);
				return;
			}
			Crud.UserWebCrud.Update(userWeb,userWebOld);
		}

		#endregion

		#region Delete

		///<summary></summary>
		public static void Delete(long userWebNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),userWebNum);
				return;
			}
			Crud.UserWebCrud.Delete(userWebNum);
		}

		#endregion
	}
}