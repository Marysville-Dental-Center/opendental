using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace OpenPmsDentalBusiness{
	///<summary></summary>
	public class EhrProvKeys{
		///<summary></summary>
		public static List<EhrProvKey> RefreshForFam(long guarantor){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<EhrProvKey>>(MethodBase.GetCurrentMethod(),guarantor);
			}
			string command="SELECT ehrprovkey.* FROM ehrprovkey,patient "
				+"WHERE ehrprovkey.PatNum=patient.PatNum "
				+"AND patient.Guarantor="+POut.Long(guarantor)+" "
				+"GROUP BY ehrprovkey.EhrProvKeyNum "
				+"ORDER BY ehrprovkey.LName,ehrprovkey.FName";
			return Crud.EhrProvKeyCrud.SelectMany(command);
		}

		///<summary>Get a list of all EhrProvKeys. Ordered by LName and then YearValue.</summary>
		public static List<EhrProvKey> GetAllKeys() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<EhrProvKey>>(MethodBase.GetCurrentMethod());
			}
			string command="SELECT ehrprovkey.* FROM ehrprovkey "
			+"ORDER BY LName,YearValue";
			return Crud.EhrProvKeyCrud.SelectMany(command);
		}

		///<summary>Get a list of all EhrProvKeys for a provider matching the given first and last name.  Ordered by year value.  Returns empty list if lName or fName is empty.</summary>
		public static List<EhrProvKey> GetKeysByFLName(string lName, string fName) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<EhrProvKey>>(MethodBase.GetCurrentMethod(),lName,fName);
			}
			if(lName==null || fName==null || 
				lName.Trim()=="" || fName.Trim()=="") {
				return new List<EhrProvKey>();
			}
			string command="SELECT ehrprovkey.* FROM ehrprovkey"
			+" WHERE ehrprovkey.LName='"+POut.String(lName)
			+"' AND ehrprovkey.FName='"+POut.String(fName)
			+"' ORDER BY ehrprovkey.YearValue DESC";
			return Crud.EhrProvKeyCrud.SelectMany(command);
		}

		///<summary>Returns true if a provider with the same last and first name passed in has ever had an EHR prov key.</summary>
		public static bool HasProvHadKey(string lName,string fName) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),lName,fName);
			}
			string command="SELECT COUNT(*) FROM ehrprovkey WHERE ehrprovkey.LName='"+POut.String(lName)+"' AND ehrprovkey.FName='"+POut.String(fName)+"'";
			return Db.GetCount(command)!="0";
		}

		///<summary>True if the ehrprovkey table has any rows, otherwise false.</summary>
		public static bool HasEhrKeys() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetBool(MethodBase.GetCurrentMethod());
			}
			string command="SELECT COUNT(*) FROM ehrprovkey";
			return PIn.Bool(Db.GetScalar(command));
		}

		///<summary></summary>
		public static long Insert(EhrProvKey ehrProvKey){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				ehrProvKey.EhrProvKeyNum=Meth.GetLong(MethodBase.GetCurrentMethod(),ehrProvKey);
				return ehrProvKey.EhrProvKeyNum;
			}
			return Crud.EhrProvKeyCrud.Insert(ehrProvKey);
		}

		///<summary></summary>
		public static void Update(EhrProvKey ehrProvKey){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				Meth.GetVoid(MethodBase.GetCurrentMethod(),ehrProvKey);
				return;
			}
			Crud.EhrProvKeyCrud.Update(ehrProvKey);
		}

		///<summary></summary>
		public static void Delete(long ehrProvKeyNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),ehrProvKeyNum);
				return;
			}
			string command= "DELETE FROM ehrprovkey WHERE EhrProvKeyNum = "+POut.Long(ehrProvKeyNum);
			Db.NonQ(command);
		}

		#region The row rule (EHR-1)
		//This open build has no licence keys (docs/specs/ehr-rebuild-plan.md, section 5). A row of this table marks the provider with its last and
		//first name as one who reports EHR. Its key text is optional, stored as typed and never checked; Open Dental's key algorithm, which lived in
		//its closed EHR.dll, is not rebuilt. A row also turns CPOE on for that provider's user (Userods.IsUserCpoe), as in Open Dental.

		///<summary>Returns "" when a row with these names and year may be saved, otherwise the reason. The last and first names must not be blank, a
		///provider must have exactly those names, and the year must be 0 to 99 (the two digits of the provider key window's year box). The names are
		///compared in the database, as the measure queries compare them (EhrMeasures.GetTable joins provider and ehrprovkey on LName and FName), so a
		///row this accepts is a row the measures find. The key text plays no part.</summary>
		public static string GetSaveError(string lName,string fName,int yearValue) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),lName,fName,yearValue);
			}
			if(string.IsNullOrWhiteSpace(lName) || string.IsNullOrWhiteSpace(fName)) {
				return Lans.g("EhrProvKeys","Please enter the provider's last name and first name.");
			}
			if(yearValue<0 || yearValue>99) {
				return Lans.g("EhrProvKeys","Invalid year, must be two digits.");
			}
			string command="SELECT COUNT(*) FROM provider WHERE provider.LName='"+POut.String(lName)+"' AND provider.FName='"+POut.String(fName)+"'";
			if(Db.GetCount(command)!="0") {
				return "";
			}
			return Lans.g("EhrProvKeys","No provider has the last name")+" '"+lName+"' "+Lans.g("EhrProvKeys","and the first name")+" '"+fName+"'. "
				+Lans.g("EhrProvKeys","Enter the names exactly as they are in the provider's record, or add the provider first.");
		}

		///<summary>The note after a provider's name in the EHR window: "" when a row has the provider's last and first name, otherwise
		///" (not marked for EHR reporting)". The same row test as GetKeysByFLName, so blank names have no row.</summary>
		public static string GetReportingNote(string lName,string fName) {
			Meth.NoCheckMiddleTierRole();
			if(GetKeysByFLName(lName,fName).Count>0) {
				return "";
			}
			return " ("+Lans.g("EhrProvKeys","not marked for EHR reporting")+")";
		}

		///<summary>Saves a row as the provider key window does. Needs the Setup permission, checked here without a message so the reason comes back;
		///then GetSaveError. A new row (IsNew) is inserted, otherwise the row is updated; ehrProvKeyOld is the row as it was loaded (not used for a new
		///row). The table's first row turns IsAlertRadiologyProcsEnabled on, as in Open Dental. One Setup security log entry gives the names and the
		///year, and the preference change when there is one. Returns "" when saved, otherwise the reason, and then nothing is written. isPrefChanged
		///is true when the preference changed: the caller signals InvalidType.Prefs.</summary>
		public static string SaveChecked(EhrProvKey ehrProvKey,EhrProvKey ehrProvKeyOld,out bool isPrefChanged) {
			Meth.NoCheckMiddleTierRole();
			isPrefChanged=false;
			string errorAuthorization=GetSetupError();
			if(errorAuthorization!="") {
				return errorAuthorization;
			}
			string error=GetSaveError(ehrProvKey.LName,ehrProvKey.FName,ehrProvKey.YearValue);
			if(error!="") {
				return error;
			}
			if(ehrProvKey.ProvKey==null) {
				ehrProvKey.ProvKey="";
			}
			if(ehrProvKey.IsNew) {
				bool isFirstKey=false;
				if(!PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled)) {
					isFirstKey=!HasEhrKeys();
				}
				Insert(ehrProvKey);
				ehrProvKey.IsNew=false;
				//The table's first row turns the radiology procedures alert on, as in Open Dental (its service raised the alert; see the plan, E-18).
				if(!PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled) && isFirstKey) {
					isPrefChanged=Prefs.UpdateBool(PrefName.IsAlertRadiologyProcsEnabled,true);
				}
				SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetLogText(null,ehrProvKey,isPrefChanged ? true : (bool?)null));
				return "";
			}
			Update(ehrProvKey);
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetLogText(ehrProvKeyOld??ehrProvKey,ehrProvKey,null));
			return "";
		}

		///<summary>Deletes a row as the provider key window does. Needs the Setup permission, checked here without a message so the reason comes back.
		///Deleting the table's last row turns IsAlertRadiologyProcsEnabled off again, as in Open Dental. One Setup security log entry gives the names
		///and the year of the row, and the preference change when there is one. Returns "" when deleted, otherwise the reason, and then nothing is
		///written. isPrefChanged is true when the preference changed: the caller signals InvalidType.Prefs.</summary>
		public static string DeleteChecked(EhrProvKey ehrProvKey,out bool isPrefChanged) {
			Meth.NoCheckMiddleTierRole();
			isPrefChanged=false;
			string errorAuthorization=GetSetupError();
			if(errorAuthorization!="") {
				return errorAuthorization;
			}
			Delete(ehrProvKey.EhrProvKeyNum);
			if(PrefC.GetBool(PrefName.IsAlertRadiologyProcsEnabled) && !HasEhrKeys()) {
				isPrefChanged=Prefs.UpdateBool(PrefName.IsAlertRadiologyProcsEnabled,false);
			}
			SecurityLogs.MakeLogEntry(EnumPermType.Setup,0,GetLogText(ehrProvKey,null,isPrefChanged ? false : (bool?)null));
			return "";
		}

		///<summary>"" when the current user has the Setup permission, otherwise the standard reason. No message box.</summary>
		private static string GetSetupError() {
			string error="";
			if(Security.IsAuthorized(EnumPermType.Setup,DateTime.MinValue,actionNotAuthorized:x => error=x)) {
				return "";
			}
			if(error=="") {
				error=Lans.g("Security","Not authorized for")+"\r\n"+GroupPermissions.GetDesc(EnumPermType.Setup);
			}
			return error;
		}

		///<summary>The Setup security log text of a saved or deleted row. ehrProvKeyOld null: the row was added; ehrProvKeyNew null: the row was
		///deleted; otherwise it was changed. isRadiologyAlertOn is the new value of IsAlertRadiologyProcsEnabled when this save or delete changed it,
		///otherwise null. The key text is not written. Pure.</summary>
		public static string GetLogText(EhrProvKey ehrProvKeyOld,EhrProvKey ehrProvKeyNew,bool? isRadiologyAlertOn) {
			Meth.NoCheckMiddleTierRole();
			string text;
			if(ehrProvKeyOld==null) {
				text="EHR reporting row added: "+Describe(ehrProvKeyNew)+".";
			}
			else if(ehrProvKeyNew==null) {
				text="EHR reporting row deleted: "+Describe(ehrProvKeyOld)+".";
			}
			else if(Describe(ehrProvKeyOld)==Describe(ehrProvKeyNew)) {
				text="EHR reporting row saved, names and year unchanged: "+Describe(ehrProvKeyNew)+".";
			}
			else {
				text="EHR reporting row changed from "+Describe(ehrProvKeyOld)+" to "+Describe(ehrProvKeyNew)+".";
			}
			if(isRadiologyAlertOn==true) {
				text+=" The radiology procedures alert (IsAlertRadiologyProcsEnabled) was turned on.";
			}
			else if(isRadiologyAlertOn==false) {
				text+=" The radiology procedures alert (IsAlertRadiologyProcsEnabled) was turned off.";
			}
			return text;
		}

		///<summary>"'LName, FName', year YY" for the security log.</summary>
		private static string Describe(EhrProvKey ehrProvKey) {
			return "'"+ehrProvKey.LName+", "+ehrProvKey.FName+"', year "+ehrProvKey.YearValue.ToString("00",CultureInfo.InvariantCulture);
		}
		#endregion The row rule (EHR-1)

		/*
		Only pull out the methods below as you need them.  Otherwise, leave them commented out.

		

		///<summary>Gets one EhrProvKey from the db.</summary>
		public static EhrProvKey GetOne(long ehrProvKeyNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT){
				return Meth.GetObject<EhrProvKey>(MethodBase.GetCurrentMethod(),ehrProvKeyNum);
			}
			return Crud.EhrProvKeyCrud.SelectOne(ehrProvKeyNum);
		}

		
		*/
	}
}