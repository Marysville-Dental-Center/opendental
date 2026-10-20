using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;
using DataConnectionBase;
using System.Text.RegularExpressions;
using EdgeExpressProps=OpenDentBusiness.ProgramProperties.PropertyDescs.EdgeExpress;

namespace OpenDentBusiness {

	///<summary></summary>
	public class ProgramProperties{
		#region CachePattern
		private class ProgramPropertyCache : CacheListAbs<ProgramProperty> {
			protected override List<ProgramProperty> GetCacheFromDb() {
				string command="SELECT * FROM programproperty";
				return Crud.ProgramPropertyCrud.SelectMany(command);
			}
			protected override List<ProgramProperty> TableToList(DataTable table) {
				return Crud.ProgramPropertyCrud.TableToList(table);
			}
			protected override ProgramProperty Copy(ProgramProperty programProperty) {
				return programProperty.Copy();
			}
			protected override DataTable ListToTable(List<ProgramProperty> listProgramPropertys) {
				return Crud.ProgramPropertyCrud.ListToTable(listProgramPropertys,"ProgramProperty");
			}
			protected override void FillCacheIfNeeded() {
				ProgramProperties.GetTableFromCache(false);
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static ProgramPropertyCache _programPropertyCache=new ProgramPropertyCache();

		public static ProgramProperty GetFirstOrDefault(Func<ProgramProperty,bool> match,bool isShort=false) {
			ProgramProperty prop=_programPropertyCache.GetFirstOrDefault(match,isShort);
			if(prop is null) {
				return prop;
			}
			prop.PropertyValue=GetHqPropertyValue(Programs.GetProgram(prop.ProgramNum),prop);
			return prop;
		}

		public static List<ProgramProperty> GetWhere(Predicate<ProgramProperty> match,bool isShort=false) {
			List<ProgramProperty> listProps=_programPropertyCache.GetWhere(match,isShort);
			foreach(ProgramProperty prop in listProps) {
				prop.PropertyValue=GetHqPropertyValue(Programs.GetProgram(prop.ProgramNum),prop);
			}
			return listProps;
		}		

		private static bool DoUseCacheValues(Program prog,ProgramProperty property) {
			//Is not an OD defined program name or is not a program HQ is concerned with enabling/disabling.
			return !HqProgram.IsInitialized() 
				|| !HqProgram.GetAll().Any(x => x.ProgramNameAsString==prog.ProgName && x.ListProperties.Any(y => y.PropertyDesc==property.PropertyDesc));
		}

		///<summary>Returns an in memory propery value override if HQ has sent one.  Uses db/cache property values if no connection to HQ or HQ has not sent an override.</summary>
		private static string GetHqPropertyValue(Program prog,ProgramProperty property) {
			string retVal="";
			if(DoUseCacheValues(prog,property)) {
				retVal=property.PropertyValue;
			}
			else {
				HqProgram hqProg=HqProgram.GetAll().Where(x=>x.ProgramNameAsString==prog.ProgName).FirstOrDefault();
				HqProgramProperty hqProp=hqProg.ListProperties.FirstOrDefault(x=>x.PropertyDesc==property.PropertyDesc);
				retVal=hqProp.PropertyValue;
			}
			return retVal;
		}

		///<summary>Used to filter any properties users don't need to see out of FormProgramLinkEdit (i.e. Url values we keep over at Hq)</summary>
		public static List<ProgramProperty> FilterProperties(Program progCur,List<ProgramProperty> listProps) {
			//If any other programs need to apply filtration, add it here.
			List<ProgramProperty> listProgramProperty=PDMP.FilterAndSortProperties(progCur,listProps);
			//Vendor job lease rows are bookkeeping for the background job host, not settings.
			listProgramProperty.RemoveAll(x => VendorDirect.VendorJobLease.IsLeaseProperty(x.PropertyDesc));
			return listProgramProperty;
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_programPropertyCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_programPropertyCache.FillCacheFromTable(table);
				return table;
			}
			return _programPropertyCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_programPropertyCache.ClearCache();
		}
		#endregion

		///<summary></summary>
		public static void Update(ProgramProperty programProp){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),programProp);
				return;
			}
			Crud.ProgramPropertyCrud.Update(programProp);
		}

		///<summary></summary>
		public static bool Update(ProgramProperty programProp,ProgramProperty programPropOld){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),programProp,programPropOld);
			}
			return Crud.ProgramPropertyCrud.Update(programProp,programPropOld);
		}

		///<summary>Returns true if the program property was updated.  False if no change needed.  Callers need to invalidate cache as needed.</summary>
		public static bool UpdateProgramPropertyWithValue(ProgramProperty programProp,string newValue) {
			Meth.NoCheckMiddleTierRole();
			if(programProp.PropertyValue==newValue) {
				return false;
			}
			programProp.PropertyValue=newValue;
			ProgramProperties.Update(programProp);
			return true;
		}

		///<summary>This is called from FormClinicEdit and from InsertOrUpdateLocalOverridePath.  PayConnect can have clinic specific login credentials,
		///so the ProgramProperties for PayConnect are duplicated for each clinic.  The properties duplicated are Username, Password, and PaymentType.
		///There's also a 'Headquarters' or no clinic set of these props with ClinicNum 0, which is the set of props inserted with each new clinic.</summary>
		public static long Insert(ProgramProperty programProp){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				programProp.ProgramPropertyNum=Meth.GetLong(MethodBase.GetCurrentMethod(),programProp);
				return programProp.ProgramPropertyNum;
			}
			return Crud.ProgramPropertyCrud.Insert(programProp);
		}

		///<summary></summary>
		public static void InsertMany(List<ProgramProperty> listProgramProps) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listProgramProps);
				return;
			}
			Crud.ProgramPropertyCrud.InsertMany(listProgramProps);
		}

		///<summary>Copies rows for a given programNum for each clinic in listClinicNums, with their IsMasked and IsHighSecurity flags, so a copied
		///secret stays masked. Rows that only exist for the practice (GetPracticeWideDescs) and vendor job leases are not copied.
		///Returns true if changes were made to the db.</summary>
		public static bool InsertForClinic(long programNum,List<long> listClinicNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),programNum,listClinicNums);
			}
			if(listClinicNums==null || listClinicNums.Count==0) {
				return false;
			}
			string whereSkip="";
			List<string> listDescsSkip=GetPracticeWideDescs(Programs.GetProgram(programNum)?.ProgName);
			if(listDescsSkip.Count>0) {
				whereSkip+="AND PropertyDesc NOT IN("+string.Join(",",listDescsSkip.Select(x => "'"+POut.String(x)+"'"))+") ";
			}
			whereSkip+="AND PropertyDesc NOT LIKE '"+POut.String(VendorDirect.VendorJobLease.PropertyDescPrefix)+"%' ";
			bool hasInsert=false;
			string command="";
			if(DataConnection.DBtype==DatabaseType.MySql) {
				command="INSERT INTO programproperty (ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) ";
				for(int i=0;i<listClinicNums.Count;i++) {
					if(i>0) {
						command+=" UNION ";
					}
					command+="SELECT ProgramNum,PropertyDesc,PropertyValue,ComputerName,"+POut.Long(listClinicNums[i])+",IsMasked,IsHighSecurity "
						+"FROM programproperty "
						+"WHERE ProgramNum="+POut.Long(programNum)+" "
						+"AND ClinicNum=0 "
						+whereSkip;
				}
				hasInsert=(Db.NonQ(command) > 0);
			}
			else {//Oracle
				command="SELECT ProgramNum,PropertyDesc,PropertyValue,ComputerName,IsMasked,IsHighSecurity "
					+"FROM programproperty "
					+"WHERE ProgramNum="+POut.Long(programNum)+" "
					+"AND ClinicNum=0 "
					+whereSkip;
				DataTable tableProgProps=Db.GetTable(command);
				//Loop through all program properties for this program.
				foreach(DataRow row in tableProgProps.Rows) {
					//Insert this program property for every single clinic passed in.
					foreach(long clinicNum in listClinicNums) {
						command="INSERT INTO programproperty (ProgramPropertyNum,ProgramNum,PropertyDesc,PropertyValue,ComputerName,ClinicNum,IsMasked,IsHighSecurity) "
							+"VALUES("
							+"(SELECT COALESCE(MAX(ProgramPropertyNum),0)+1 FROM programproperty),"
							+row["ProgramNum"].ToString()+","
							+"'"+row["PropertyDesc"].ToString()+"',"
							+"'"+row["PropertyValue"].ToString()+"',"
							+"'"+row["ComputerName"].ToString()+"',"
							+POut.Long(clinicNum)+","
							+POut.Bool(PIn.Bool(row["IsMasked"].ToString()))+","
							+POut.Bool(PIn.Bool(row["IsHighSecurity"].ToString()))+")";
						Db.NonQ(command);
						hasInsert=true;
					}
				}
			}
			return hasInsert;
		}

		///<summary>Property descriptions that only exist for the practice (ClinicNum 0) and are never copied to a clinic. progName may be null.</summary>
		public static List<string> GetPracticeWideDescs(string progName) {
			Meth.NoCheckMiddleTierRole();
			List<string> listDescs=new List<string>();
			if(progName==ProgramName.PayConnect.ToString()) {
				//The PayConnect 2 integrator API key is practice-wide; only the merchant Secret is per clinic. The PayConnect package owns the
				//constant for this name; it is repeated here so this list doesn't depend on that package.
				listDescs.Add("API Key");
			}
			return listDescs;
		}

		///<summary>Returns a list of ProgramProperties with the specified programNum and the specified clinicNum from the cache.
		///To get properties when clinics are not enabled or properties for 'Headquarters' use clinicNum 0.
		///Does not include path overrides.</summary>
		public static List<ProgramProperty> GetListForProgramAndClinic(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return ProgramProperties.GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==clinicNum && x.PropertyDesc!="");
		}

		///<summary>Returns a List of ProgramProperties attached to the specified programNum with the given clinicnum.  
		///Includes the default program properties as well (ClinicNum==0).</summary>
		public static List<ProgramProperty> GetListForProgramAndClinicWithDefault(long programNum,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			List<ProgramProperty> listClinicProperties=GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==clinicNum);
			if(clinicNum==0) {
				return listClinicProperties;//return the defaults cause ClinicNum of 0 is default.
			}
			//Get all the defaults and return a list of defaults mixed with overrides.
			List<ProgramProperty> listClinicAndDefaultProperties=GetWhere(x => x.ProgramNum==programNum && x.ClinicNum==0
				&& !listClinicProperties.Any(y => y.PropertyDesc==x.PropertyDesc));
			listClinicAndDefaultProperties.AddRange(listClinicProperties);
			return listClinicAndDefaultProperties;//Clinic users need to have all properties, defaults with the clinic overrides.
		}

		///<summary>Returns the property value of the clinic override or default program property if no clinic override is found.</summary>
		public static string GetPropValForClinicOrDefault(long programNum,string desc,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			return GetListForProgramAndClinicWithDefault(programNum,clinicNum).FirstOrDefault(x => x.PropertyDesc==desc).PropertyValue;
		}

		#region Vendor-direct helpers
		///<summary>Returns the value for the clinic's row, else the ClinicNum 0 row, else "". Reads the cache and never throws, so a workstation never
		///fails just because nobody has opened the integration's setup window yet. Every new vendor-direct reader uses this.</summary>
		public static string GetValOrDefault(long programNum,string desc,long clinicNum) {
			Meth.NoCheckMiddleTierRole();
			try {
				List<ProgramProperty> listProgramProperties=GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc==desc
					&& (x.ClinicNum==clinicNum || x.ClinicNum==0));
				ProgramProperty programProperty=listProgramProperties.Where(x => x.ClinicNum==clinicNum).OrderBy(x => x.ProgramPropertyNum).FirstOrDefault()
					??listProgramProperties.Where(x => x.ClinicNum==0).OrderBy(x => x.ProgramPropertyNum).FirstOrDefault();
				return programProperty?.PropertyValue??"";
			}
			catch(Exception ex) {
				ex.DoNothing();
				return "";
			}
		}

		///<summary>Returns the row for programNum, desc and clinicNum straight from the database (not the cache), the one with the lowest key if there
		///are several, or null. Path overrides (ComputerName rows) are never returned for a non-blank desc because their desc is blank.</summary>
		public static ProgramProperty GetOneFromDb(long programNum,string desc,long clinicNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProgramProperty>(MethodBase.GetCurrentMethod(),programNum,desc,clinicNum);
			}
			string command="SELECT * FROM programproperty "
				+"WHERE ProgramNum="+POut.Long(programNum)+" "
				+"AND PropertyDesc='"+POut.String(desc)+"' "
				+"AND ClinicNum="+POut.Long(clinicNum)+" "
				+"ORDER BY ProgramPropertyNum";
			return Crud.ProgramPropertyCrud.SelectMany(command).FirstOrDefault();
		}

		///<summary>Inserts the row for programNum, desc and clinicNum with defaultValue if it doesn't exist, and returns the row (existing or new).
		///Idempotent: if two workstations insert at the same moment, the duplicate with the higher key is deleted. Flags are only ever raised: an
		///existing row gets IsMasked or IsHighSecurity set when the caller asks for it (secret rows are always both). Does not refresh the cache;
		///the caller signals InvalidType.Programs once it is done.</summary>
		public static ProgramProperty InsertIfMissing(long programNum,string desc,long clinicNum,string defaultValue,bool isMasked,bool isHighSecurity) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<ProgramProperty>(MethodBase.GetCurrentMethod(),programNum,desc,clinicNum,defaultValue,isMasked,isHighSecurity);
			}
			if(string.IsNullOrEmpty(desc)) {
				throw new ArgumentException("A property description is required.",nameof(desc));
			}
			ProgramProperty programProperty=GetOneFromDb(programNum,desc,clinicNum);
			if(programProperty==null) {
				programProperty=new ProgramProperty();
				programProperty.ProgramNum=programNum;
				programProperty.PropertyDesc=desc;
				programProperty.PropertyValue=defaultValue??"";
				programProperty.ComputerName="";
				programProperty.ClinicNum=clinicNum;
				programProperty.IsMasked=isMasked;
				programProperty.IsHighSecurity=isHighSecurity;
				Crud.ProgramPropertyCrud.Insert(programProperty);
				//Another workstation may have inserted the same row at the same moment. Keep the lowest key.
				string command="SELECT ProgramPropertyNum FROM programproperty "
					+"WHERE ProgramNum="+POut.Long(programNum)+" "
					+"AND PropertyDesc='"+POut.String(desc)+"' "
					+"AND ClinicNum="+POut.Long(clinicNum)+" "
					+"ORDER BY ProgramPropertyNum";
				List<long> listNums=Db.GetListLong(command);
				if(listNums.Count>1) {
					command="DELETE FROM programproperty "
						+"WHERE ProgramPropertyNum IN("+string.Join(",",listNums.Skip(1).Select(x => POut.Long(x)))+")";
					Db.NonQ(command);
				}
				return GetOneFromDb(programNum,desc,clinicNum);
			}
			if((isMasked && !programProperty.IsMasked) || (isHighSecurity && !programProperty.IsHighSecurity)) {
				string command="UPDATE programproperty SET "
					+"IsMasked="+POut.Bool(programProperty.IsMasked || isMasked)+","
					+"IsHighSecurity="+POut.Bool(programProperty.IsHighSecurity || isHighSecurity)+" "
					+"WHERE ProgramPropertyNum="+POut.Long(programProperty.ProgramPropertyNum);
				Db.NonQ(command);
				programProperty.IsMasked=programProperty.IsMasked || isMasked;
				programProperty.IsHighSecurity=programProperty.IsHighSecurity || isHighSecurity;
			}
			return programProperty;
		}

		///<summary>Compare-and-swap: sets PropertyValue to newValue only if it still equals oldValue (exact, case-sensitive). Returns true if the row
		///now holds newValue because of this call; false if another workstation changed it first or the row is gone. Works on osd1$ ciphertexts
		///because they are deterministic. Used for rotating refresh tokens and job leases. Does not refresh the cache.</summary>
		public static bool UpdateValueIfUnchanged(long programPropertyNum,string oldValue,string newValue) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),programPropertyNum,oldValue,newValue);
			}
			oldValue=oldValue??"";
			newValue=newValue??"";
			if(oldValue==newValue) {
				//Nothing to write. Report whether the row holds that value.
				ProgramProperty programProperty=Crud.ProgramPropertyCrud.SelectOne(programPropertyNum);
				return programProperty!=null && (programProperty.PropertyValue??"")==oldValue;
			}
			//BINARY makes the comparison exact: the column's collation ignores letter case, and ciphertexts differ by case.
			string compare=(DataConnection.DBtype==DatabaseType.MySql) ? "BINARY PropertyValue=BINARY " : "PropertyValue=";
			string command="UPDATE programproperty SET PropertyValue="+DbHelper.ParamChar+"paramNewValue "
				+"WHERE ProgramPropertyNum="+POut.Long(programPropertyNum)+" "
				+"AND "+compare+DbHelper.ParamChar+"paramOldValue";
			OdSqlParameter paramNewValue=new OdSqlParameter("paramNewValue",OdDbType.Text,POut.StringParam(newValue));
			OdSqlParameter paramOldValue=new OdSqlParameter("paramOldValue",OdDbType.Text,POut.StringParam(oldValue));
			return Db.NonQ(command,paramNewValue,paramOldValue)>0;
		}
		#endregion Vendor-direct helpers

		///<summary>Returns a list of ProgramProperties attached to the specified programNum.  Does not include path overrides.
		///Uses thread-safe caching pattern.  Each call to this method creates an copy of the entire ProgramProperty cache.</summary>
		public static List<ProgramProperty> GetForProgram(long programNum) {
			Meth.NoCheckMiddleTierRole();
			return GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc!="").OrderBy(x => x.ClinicNum).ThenBy(x => x.ProgramPropertyNum).ToList();
		}

		///<summary>Sets the program property for all clinics.  Returns the number of rows changed.</summary>
		public static long SetProperty(long programNum,string desc,string propval) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetLong(MethodBase.GetCurrentMethod(),programNum,desc,propval);
			}
			string command=$@"UPDATE programproperty SET PropertyValue='{POut.String(propval)}'
				WHERE ProgramNum={POut.Long(programNum)}
				AND PropertyDesc='{POut.String(desc)}'";
			return Db.NonQ(command);
		}

		///<summary>After GetForProgram has been run, this gets one of those properties.  DO NOT MODIFY the returned property.  Read only.</summary>
		public static ProgramProperty GetCur(List<ProgramProperty> listForProgram,string desc) {
			Meth.NoCheckMiddleTierRole();
			return listForProgram.FirstOrDefault(x => x.PropertyDesc==desc);
		}

		///<summary>Throws exception if program property is not found. This indicates database corruption, so no need to surround with try/catch.</summary>
		public static string GetPropVal(long programNum,string desc) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty programProperty=GetFirstOrDefault(x => x.ProgramNum==programNum && x.PropertyDesc==desc);
			if(programProperty!=null) {
				return programProperty.PropertyValue;
			}
			throw new ApplicationException("Property not found: "+desc);
		}

		public static string GetPropVal(ProgramName programName,string desc) {
			Meth.NoCheckMiddleTierRole();
			long programNum=Programs.GetProgramNum(programName);
			return GetPropVal(programNum,desc);
		}

		///<summary>Returns the PropertyVal for programNum and clinicNum specified with the description specified.  If the property doesn't exist,
		///returns an empty string.  For the PropertyVal for 'Headquarters' or clincs not enabled, use clinicNum 0.</summary>
		public static string GetPropVal(long programNum,string desc,long clinicNum) {
			return GetPropValFromList(ProgramProperties.GetWhere(x => x.ProgramNum==programNum),desc,clinicNum);
		}

		///<summary>Returns the PropertyVal from the list by PropertyDesc and ClinicNum.
		///For the 'Headquarters' or for clinics not enabled, omit clinicNum or send clinicNum 0.  If not found returns an empty string.
		///Primarily used when a local list has been copied from the cache and may differ from what's in the database.  Also possibly useful if dealing with a filtered list </summary>
		public static string GetPropValFromList(List<ProgramProperty> listProps,string propertyDesc,long clinicNum=0) {
			string retval="";
			ProgramProperty prop=listProps.Where(x => x.ClinicNum==clinicNum).Where(x => x.PropertyDesc==propertyDesc).FirstOrDefault();
			if(prop!=null) {
				retval=prop.PropertyValue;
			}
			return retval;
		}

		///<summary>Returns the property with the matching description from the provided list.  Null if the property cannot be found by the description.</summary>
		public static ProgramProperty GetPropByDesc(string propertyDesc,List<ProgramProperty> listProperties) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty property=null;
			for(int i=0;i<listProperties.Count;i++) {
				if(listProperties[i].PropertyDesc==propertyDesc) {
					property=listProperties[i];
					break;
				}
			}
			return property;
		}

		///<summary>Returns the property with the matching description from the provided list.  Null if the property cannot be found by the description.</summary>
		public static ProgramProperty GetPropForProgByDesc(long programNum,string propertyDesc) {
			Meth.NoCheckMiddleTierRole();
			return GetForProgram(programNum).FirstOrDefault(x => x.PropertyDesc==propertyDesc);
		}

		///<summary>Returns the property with the matching description from the provided list.  Null if the property cannot be found by the description.</summary>
		public static ProgramProperty GetPropForProgByDesc(long programNum,string propertyDesc,long clinicNum=0) {
			Meth.NoCheckMiddleTierRole();
			return GetForProgram(programNum).FirstOrDefault(x => x.PropertyDesc==propertyDesc && x.ClinicNum==clinicNum);
		}

		///<summary>Used in FormUAppoint to get frequent and current data.</summary>
		public static string GetValFromDb(long programNum,string desc) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetString(MethodBase.GetCurrentMethod(),programNum,desc);
			}
			string command="SELECT PropertyValue FROM programproperty WHERE ProgramNum="+POut.Long(programNum)
				+" AND PropertyDesc='"+POut.String(desc)+"'";
			DataTable table=Db.GetTable(command);
			if(table.Rows.Count==0){
				return "";
			}
			return table.Rows[0][0].ToString();
		}

		///<summary>Returns the path override for the current computer and the specified programNum.  Returns empty string if no override found.</summary>
		public static string GetLocalPathOverrideForProgram(long programNum) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty programProperty=GetFirstOrDefault(x => x.ProgramNum==programNum
					&& x.PropertyDesc==""
					&& x.ComputerName.ToUpper()==ODEnvironment.MachineName.ToUpper());
			return (programProperty==null ? "" : programProperty.PropertyValue);
		}

		///<summary>This will insert or update a local path override property for the specified programNum.</summary>
		public static void InsertOrUpdateLocalOverridePath(long programNum,string newPath) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty programProperty=GetFirstOrDefault(x => x.ProgramNum==programNum
					&& x.PropertyDesc==""
					&& x.ComputerName.ToUpper()==ODEnvironment.MachineName.ToUpper());
			if(programProperty!=null) {
				programProperty.PropertyValue=newPath;
				ProgramProperties.Update(programProperty);
				return;//Will only be one override per computer per program.
			}
			//Path override does not exist for the current computer so create a new one.
			ProgramProperty pp=new ProgramProperty();
			pp.ProgramNum=programNum;
			pp.PropertyValue=newPath;
			pp.ComputerName=ODEnvironment.MachineName.ToUpper();
			ProgramProperties.Insert(pp);
		}

		///<summary>Syncs list against cache copy of program properties.  listProgPropsNew should never include local path overrides (PropertyDesc=="").
		///This sync uses the cache copy of program properties rather than a stale list because we want to make sure we never have duplicate properties
		///and concurrency isn't really an issue.</summary>
		public static bool Sync(List<ProgramProperty> listProgPropsNew,long programNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listProgPropsNew,programNum);
			}
			//prevents delete of program properties for clinics added while editing program properties.
			List<long> listClinicNums = listProgPropsNew.Select(x => x.ClinicNum).Distinct().ToList();
			List<ProgramProperty> listProgPropsDb=ProgramProperties.GetWhere(x => x.ProgramNum==programNum
				&& x.PropertyDesc!=""
				&& listClinicNums.Contains(x.ClinicNum));
			return Crud.ProgramPropertyCrud.Sync(listProgPropsNew,listProgPropsDb);
		}

		///<summary>Syncs list against cache copy of program properties.  listProgPropsNew should never include local path overrides (PropertyDesc=="").
		///This sync uses the cache copy of program properties rather than a stale list because we want to make sure we never have duplicate properties
		///and concurrency isn't really an issue. This WILL delete program properties from the database if missing from listProgPropsNew for the specified
		///clinics.  Only include clinics to which the current user is allowed access.</summary>
		public static void Sync(List<ProgramProperty> listProgPropsNew,long programNum,List<long> listClinicNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listProgPropsNew,programNum,listClinicNums);
				return;
			}
			List<ProgramProperty> listProgPropsDb=GetWhere(x => x.ProgramNum==programNum && x.PropertyDesc!="" && listClinicNums.Contains(x.ClinicNum));
			Crud.ProgramPropertyCrud.Sync(listProgPropsNew,listProgPropsDb);
		}

		///<summary>Exception means failed. Return means success. paymentsAllowed should be check after return. If false then assume payments cannot be made for this clinic.</summary>
		public static void GetXWebCreds(long clinicNum,out OpenDentBusiness.WebTypes.Shared.XWeb.WebPaymentProperties xwebProperties) {
			string xWebID;
			string authKey;
			string terminalID;
			long paymentTypeDefNum;
			string paymentTypeDefString;
			bool isPaymentsAllowed;
			bool isXWeb;
			xwebProperties=new WebTypes.Shared.XWeb.WebPaymentProperties();
			Meth.NoCheckMiddleTierRole();
			//Secure arguments are held in the db.
			Program prog=Programs.GetCur(ProgramName.EdgeExpress);
			if(prog!=null && prog.Enabled) {
				List<ProgramProperty> listEdgeExpressProperties=GetListForProgramAndClinic(prog.ProgramNum,clinicNum);
				xWebID=GetPropValFromList(listEdgeExpressProperties,PropertyDescs.EdgeExpress.XWebID,clinicNum);
				authKey=GetPropValFromList(listEdgeExpressProperties,PropertyDescs.EdgeExpress.AuthKey,clinicNum);
				terminalID=GetPropValFromList(listEdgeExpressProperties,PropertyDescs.EdgeExpress.TerminalID,clinicNum);
				paymentTypeDefString=GetPropValFromList(listEdgeExpressProperties,PropertyDescs.EdgeExpress.PaymentType,clinicNum);
				isPaymentsAllowed=PIn.Bool(GetPropValFromList(listEdgeExpressProperties,PropertyDescs.EdgeExpress.IsOnlinePaymentsEnabled,clinicNum));
				isXWeb=false;
			}
			else {
				prog=Programs.GetCur(ProgramName.Xcharge);
				if(prog==null) {
					throw new ODException("X-Charge program link not found.",ODException.ErrorCodes.XWebProgramProperties);
				}
				if(!prog.Enabled) { //EdgeExpress and XCharge not turned on.
					throw new ODException("EdgeExpress program link is disabled.",ODException.ErrorCodes.XWebProgramProperties);
				}
				List<ProgramProperty> listXchargeProperties=GetListForProgramAndClinic(prog.ProgramNum,clinicNum);
				xWebID=GetPropValFromList(listXchargeProperties,"XWebID",clinicNum);
				authKey=GetPropValFromList(listXchargeProperties,"AuthKey",clinicNum);
				terminalID=GetPropValFromList(listXchargeProperties,"TerminalID",clinicNum);
				paymentTypeDefString=GetPropValFromList(listXchargeProperties,"PaymentType",clinicNum);
				isPaymentsAllowed=PIn.Bool(GetPropValFromList(listXchargeProperties,"IsOnlinePaymentsEnabled",clinicNum));
				isXWeb=true;
			}
			//The Auth Key is stored as osd1$ once the setup window saves it; Open Dental stored it as plain text, which is still accepted.
			//An unreadable value counts as missing.
			authKey=VendorDirect.SecretValue.ReadOrBlank(authKey,VendorDirect.LegacyPolicy.AcceptPlaintext);
			//Validate ALL XWebID, AuthKey, and TerminalID.  Each is required for X-Web to work.
			if(string.IsNullOrEmpty(xWebID) || string.IsNullOrEmpty(authKey) || string.IsNullOrEmpty(terminalID) ||
				!long.TryParse(paymentTypeDefString,out paymentTypeDefNum))
			{
				throw new ODException("X-Web program properties not found.",ODException.ErrorCodes.XWebProgramProperties);
			}
			//XWeb ID must be 12 digits, Auth Key 32 alphanumeric characters, and Terminal ID 8 digits.
			if(!Regex.IsMatch(xWebID,"^[0-9]{12}$")
				||!Regex.IsMatch(authKey,"^[A-Za-z0-9]{32}$")
				||!Regex.IsMatch(terminalID,"^[0-9]{8}$"))
			{
				throw new ODException("X-Web program properties not valid.",ODException.ErrorCodes.XWebProgramProperties);
			}
			xwebProperties.XWebID=xWebID;
			xwebProperties.TerminalID=terminalID;
			xwebProperties.AuthKey=authKey;
			xwebProperties.PaymentTypeDefNum=paymentTypeDefNum;
			xwebProperties.IsPaymentsAllowed=isPaymentsAllowed;
			xwebProperties.IsXWeb=isXWeb;
		}

		///<summary>Exception means failed. Return means success. paymentsAllowed should be check after return. If false then assume payments cannot be made for this clinic.</summary>
		public static void GetPayConnectPatPortalCreds(long clinicNum,out PayConnect.WebPaymentProperties payConnectProps) {
			Meth.NoCheckMiddleTierRole();
			//Secure arguments are held in the db.
			payConnectProps=new PayConnect.WebPaymentProperties();
			OpenDentBusiness.Program programPayConnect=OpenDentBusiness.Programs.GetCur(OpenDentBusiness.ProgramName.PayConnect);
			if(programPayConnect==null) { //PayConnect not setup.
				throw new ODException("PayConnect program link not found.",ODException.ErrorCodes.PayConnectProgramProperties);
			}
			if(!programPayConnect.Enabled) { //PayConnect not turned on.
				throw new ODException("PayConnect program link is disabled.",ODException.ErrorCodes.PayConnectProgramProperties);
			}
			//Validate the online token, since it is requiored for PayConnect online payments to work.
			List<OpenDentBusiness.ProgramProperty> listPayConnectProperties=OpenDentBusiness.ProgramProperties.GetListForProgramAndClinic(programPayConnect.ProgramNum,clinicNum);
			payConnectProps.Token=OpenDentBusiness.ProgramProperties.GetPropValFromList(listPayConnectProperties,PayConnect.ProgramProperties.PatientPortalPaymentsToken,clinicNum);
			payConnectProps.ProgramVersion=PIn.Int(OpenDentBusiness.ProgramProperties.GetPropValFromList(listPayConnectProperties,PayConnect.ProgramProperties.ProgramVersion,clinicNum));
			if(payConnectProps.ProgramVersion==1 && string.IsNullOrEmpty(payConnectProps.Token)) {
				//PayConnect version 1 uses the Token property.
				throw new ODException("PayConnect online token not found.",ODException.ErrorCodes.PayConnectProgramProperties);
			}
			string paymentsAllowedVal=OpenDentBusiness.ProgramProperties.GetPropValFromList(listPayConnectProperties,PayConnect.ProgramProperties.PatientPortalPaymentsEnabled,clinicNum);
			payConnectProps.IsPaymentsAllowed=OpenDentBusiness.PIn.Bool(paymentsAllowedVal);
		}

		/// <summary>Exception means failed. Return means success. paySimpleProps should be checked upon return. If false then assume payments cannot be made for this clinic.</summary>
		public static void GetPaySimpleOnlinePaymentCreds(long clinicNum,out PaySimple.WebPaymentProperties paySimpleProps) {
			Meth.NoCheckMiddleTierRole();
			paySimpleProps=new PaySimple.WebPaymentProperties();
			OpenDentBusiness.Program programPaySimple=OpenDentBusiness.Programs.GetCur(OpenDentBusiness.ProgramName.PaySimple);
			if(programPaySimple==null) {
				throw new ODException("PaySimple program link not found.",ODException.ErrorCodes.PaySimpleProgramProperties);
			}
			if(!programPaySimple.Enabled) {
				throw new ODException("PaySimple program link is disabled.",ODException.ErrorCodes.PaySimpleProgramProperties);
			}
			List<OpenDentBusiness.ProgramProperty> listPaySimpleProperties=OpenDentBusiness.ProgramProperties.GetListForProgramAndClinic(programPaySimple.ProgramNum,clinicNum);
			paySimpleProps.Token=OpenDentBusiness.ProgramProperties.GetPropValFromList(listPaySimpleProperties,PaySimple.PropertyDescs.PaySimpleApiKey,clinicNum);
			//osd1$ once saved by the setup window; Open Dental's plain-text keys are still accepted. An unreadable value counts as missing.
			paySimpleProps.Token=VendorDirect.SecretValue.ReadOrBlank(paySimpleProps.Token,VendorDirect.LegacyPolicy.AcceptPlaintext);
			if(string.IsNullOrEmpty(paySimpleProps.Token)) {
				throw new ODException("PaySimple API Key not found.",ODException.ErrorCodes.PaySimpleProgramProperties);
			}
			string paymentsAllowedVal=OpenDentBusiness.ProgramProperties.GetPropValFromList(listPaySimpleProperties,PaySimple.PropertyDescs.PaySimpleIsOnlinePaymentsEnabled,clinicNum);
			paySimpleProps.IsOnlinePaymentAllowed=OpenDentBusiness.PIn.Bool(paymentsAllowedVal);
		}

		///<summary>Returns an IsOnlinePaymentsEnabled program property if one of the programs (excluding the passed in program) with online payments capability has it enabled, returns null if they do not. We exclude the passed in program because we are concerned about the other programs being enabled before deciding what to do with the passed in program.</summary>
		public static ProgramProperty GetOnlinePaymentsEnabledForClinic(long clinicNum,ProgramName programName) {
			Meth.NoCheckMiddleTierRole();
			ProgramProperty xchargeOnlinePaymentEnabled=ProgramProperties.GetWhere(x =>
				x.ProgramNum==Programs.GetCur(ProgramName.Xcharge).ProgramNum
				&& x.ClinicNum==clinicNum
				&& x.PropertyDesc=="IsOnlinePaymentsEnabled")
				.FirstOrDefault();
			ProgramProperty edgeExpressOnlinePaymentEnabled=ProgramProperties.GetWhere(x =>
				x.ProgramNum==Programs.GetCur(ProgramName.EdgeExpress).ProgramNum
				&& x.ClinicNum==clinicNum
				&& x.PropertyDesc==EdgeExpressProps.IsOnlinePaymentsEnabled)
				.FirstOrDefault();
			ProgramProperty payConnectOnlinePaymentEnabled=ProgramProperties.GetWhere(x =>
				x.ProgramNum==Programs.GetCur(ProgramName.PayConnect).ProgramNum
				&& x.ClinicNum==clinicNum
				&& x.PropertyDesc==PayConnect.ProgramProperties.PatientPortalPaymentsEnabled)
				.FirstOrDefault();
			ProgramProperty paySimpleOnlinePaymentsEnabled=ProgramProperties.GetWhere(x =>
			x.ProgramNum==Programs.GetCur(ProgramName.PaySimple).ProgramNum
				&& x.ClinicNum==clinicNum
				&& x.PropertyDesc==PaySimple.PropertyDescs.PaySimpleIsOnlinePaymentsEnabled)
				.FirstOrDefault();
			bool hasXchargeOnlinePaymentEnabled=xchargeOnlinePaymentEnabled!=null && xchargeOnlinePaymentEnabled.PropertyValue=="1";
			bool hasEdgeExpressOnlinePaymentEnabled=edgeExpressOnlinePaymentEnabled!=null && edgeExpressOnlinePaymentEnabled.PropertyValue=="1";
			bool hasPayConnectOnlinePaymentEnabled=payConnectOnlinePaymentEnabled!=null && payConnectOnlinePaymentEnabled.PropertyValue=="1";
			bool hasPaySimpleOnlinePaymentsEnabled=paySimpleOnlinePaymentsEnabled!=null && paySimpleOnlinePaymentsEnabled.PropertyValue=="1";
			//If the program is not the one passed in and online payments are enabled, that is the one we'll return. There can only be one enabled per clinic.
			//Otherwise, there aren't any enabled.
			ProgramProperty programProperty=null;
			if(programName!=ProgramName.EdgeExpress && hasEdgeExpressOnlinePaymentEnabled) {
				programProperty=edgeExpressOnlinePaymentEnabled;
			}
			else if(programName!=ProgramName.PayConnect && hasPayConnectOnlinePaymentEnabled) {
				programProperty=payConnectOnlinePaymentEnabled;
			}
			else if(programName!=ProgramName.Xcharge && hasXchargeOnlinePaymentEnabled) {
				programProperty=xchargeOnlinePaymentEnabled;
			}
			else if(programName!=ProgramName.PaySimple && hasPaySimpleOnlinePaymentsEnabled) {
				programProperty=paySimpleOnlinePaymentsEnabled;
			}
			return programProperty;
		}

		///<summary>QuickBooks Online Accounts and Class Refs are stored as ',' separated Name/Id pairs. Each pair is separated by a '|'. Returns the Id for the passed in name of the Account or Class Ref.</summary>
		public static string GetQuickBooksOnlineEntityIdByName(string propertyValue,string entityName) {
			Meth.NoCheckMiddleTierRole();
			string[] arrayEntityNameIdPairs=propertyValue.Split('|');
			for(int i=0;i<arrayEntityNameIdPairs.Length;i++) {
				string[] arrayNameAndId=arrayEntityNameIdPairs[i].Split(',');
				if(arrayNameAndId[0]==entityName) {
					return arrayNameAndId[1];
				}
			}
			return "";
		}

		///<summary>QuickBooks Online Accounts and Class Refs are stored as ',' separated Name/Id pairs. Each pair is separated by a '|'. Returns the name
		///stored for the passed in Id, or "" if none. Matching by Id survives a rename in QuickBooks Online.</summary>
		public static string GetQuickBooksOnlineEntityNameById(string propertyValue,string entityId) {
			Meth.NoCheckMiddleTierRole();
			if(propertyValue.IsNullOrEmpty() || entityId.IsNullOrEmpty()) {
				return "";
			}
			string[] arrayEntityNameIdPairs=propertyValue.Split('|');
			for(int i=0;i<arrayEntityNameIdPairs.Length;i++) {
				string[] arrayNameAndId=arrayEntityNameIdPairs[i].Split(',');
				if(arrayNameAndId.Length>1 && arrayNameAndId[arrayNameAndId.Length-1]==entityId) {
					return string.Join(",",arrayNameAndId.Take(arrayNameAndId.Length-1));
				}
			}
			return "";
		}

		///<summary>QuickBooks Online Accounts and Class Refs are stored as ',' separated Name/Id pairs. Each pair is separated by a '|'. Returns a list of all Account or Class Ref names.</summary>
		public static List<string> GetQuickBooksOnlineEntityNames(string propertyValue) {
			Meth.NoCheckMiddleTierRole();
			List<string> listNames=new List<string>();
			if(propertyValue.IsNullOrEmpty()) {
				return listNames;
			}
			string[] arrayEntities=propertyValue.Split('|');
			for(int i=0;i<arrayEntities.Length;i++) {
				listNames.Add(arrayEntities[i].Split(',')[0]);
			}
			listNames.Sort();
			return listNames;
		}

		///<summary>Returns true if the user has the ManageHighSecurityProgProperties permission or no property in the program is marked high security. 
		///Otherwise returns false. Suppress the message that Security.IsAuthorized might show.</summary>
		public static bool CanEditProperties(List<ProgramProperty> listProperties, bool suppressMesssage=true) {
			if(listProperties.Any(x => x.IsHighSecurity)) {
				return Security.IsAuthorized(EnumPermType.ManageHighSecurityProgProperties,suppressMesssage);
			}
			return true;
		}

		///<summary>Deletes a given programproperty from the table based upon its programPropertyNum.
		///Must have a property description in the GetDeletablePropertyDescriptions() list to delete</summary>
		public static void Delete(ProgramProperty prop) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),prop);
				return;
			}
			if(!GetDeletablePropertyDescriptions().Contains(prop.PropertyDesc)) {
				throw new Exception("Not allowed to delete the ProgramProperty with a description of: "+prop.PropertyDesc);
			}
			string command="DELETE FROM programproperty WHERE ProgramPropertyNum="+POut.Long(prop.ProgramPropertyNum);
			Db.NonQ(command);
		}

		///<summary>Deleting from the ProgramProperty table should be considered dangerous and extremely deliberate, anyone looking to do so must
		///explicitly add their condition to this method in the future.</summary>
		private static List<string> GetDeletablePropertyDescriptions() {
			return new List<string>() {
				PropertyDescs.ClinicHideButton
			};
		}

		public class PropertyDescs {
			public const string ImageFolder="Image Folder";
			public const string PatOrChartNum="Enter 0 to use PatientNum, or 1 to use ChartNum";
			public const string Username="Username";
			public const string Password="Password";
			public const string ClinicHideButton="ClinicHideButton";
			///<summary>No longer read. Open Dental showed partner buttons for program links that were not enabled unless the office set this to 1.
			///Integration UI now only shows when the program link is enabled. The rows are kept for schema compatibility and hidden from the
			///property grids.</summary>
			public const string DisableAdvertising="Disable Advertising";
			///<summary>No longer read. The Open Dental HQ override of DisableAdvertising. Kept and hidden for the same reason.</summary>
			public const string DisableAdvertisingHQ="Disable Advertising HQ";

			//Prevents this class from being instansiated.
			private PropertyDescs() { }

			public static class TransWorld {
				public const string SyncExcludePosAdjType="SyncExcludePosAdjType";
				public const string SyncExcludeNegAdjType="SyncExcludeNegAdjType";
			}
			public static class XCharge {
				public const string XChargeForceRecurringCharge="XChargeForceRecurringCharge";
				public const string XChargePreventSavingNewCC="XChargePreventSavingNewCC";
			}
			public static class CareCredit {
				public const string CareCreditPaymentType="CareCreditPaymentType";
				public const string CareCreditOAuthToken="CareCreditOAuthToken";
				public const string CareCreditQSBatchEnabled="QSBatchEnabled";
				public const string CareCreditQSBatchDays="CareCreditQSBatchDays";
				public const string CareCreditPatField="CareCreditPatField";
				public const string CareCreditPatFieldPreApprovalAmt="CareCreditPatFieldPreApprovalAmt";
				public const string CareCreditPatFieldAvailableCredit="CareCreditPatFieldAvailableCredit";
				public const string CareCreditIsMerchantNumberByProv="CareCreditIsMerchantNumberByProv";
				public const string CareCreditMerchantNumber="CareCreditMerchantNumber";
				public const string CareCreditPartnerCode="CareCreditPartnerCode";

				public static string GetForPatFieldType(PatFieldType patFieldType){
					if(patFieldType==PatFieldType.CareCreditStatus){
						return CareCreditPatField;
					}
					else if(patFieldType==PatFieldType.CareCreditPreApprovalAmt){
						return CareCreditPatFieldPreApprovalAmt;
					}
					else if(patFieldType==PatFieldType.CareCreditAvailableCredit){
						return CareCreditPatFieldAvailableCredit;
					}
					return "";
				}
			}
			public static class EdgeExpress {
				public const string ForceRecurringCharge="EdgeExpressForceRecurringCharge";
				public const string PreventSavingNewCC="EdgeExpressPreventSavingNewCC";
				public const string PromptSignature="EdgeExpressPromptSignature";
				public const string PrintReceipt="EdgeExpressPrintReceipt";
				public const string XWebID="EdgeExpressXWebID";
				public const string AuthKey="EdgeExpressAuthKey";
				public const string TerminalID="EdgeExpressTerminalID";
				public const string PaymentType="EdgeExpressPaymentType";
				public const string IsOnlinePaymentsEnabled="EdgeExpressIsOnlinePaymentsEnabled";
			}
		}
	}
}










