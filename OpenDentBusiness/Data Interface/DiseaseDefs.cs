using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace OpenDentBusiness {
	///<summary></summary>
	public class DiseaseDefs {
		#region CachePattern

		private class DiseaseDefCache : CacheListAbs<DiseaseDef> {
			protected override List<DiseaseDef> GetCacheFromDb() {
				string command="SELECT * FROM diseasedef ORDER BY ItemOrder";
				return Crud.DiseaseDefCrud.SelectMany(command);
			}
			protected override List<DiseaseDef> TableToList(DataTable table) {
				return Crud.DiseaseDefCrud.TableToList(table);
			}
			protected override DiseaseDef Copy(DiseaseDef diseaseDef) {
				return diseaseDef.Copy();
			}
			protected override DataTable ListToTable(List<DiseaseDef> listDiseaseDefs) {
				return Crud.DiseaseDefCrud.ListToTable(listDiseaseDefs,"DiseaseDef");
			}
			protected override void FillCacheIfNeeded() {
				DiseaseDefs.GetTableFromCache(false);
			}
			protected override bool IsInListShort(DiseaseDef diseaseDef) {
				return !diseaseDef.IsHidden;
			}
		}
		
		///<summary>The object that accesses the cache in a thread-safe manner.</summary>
		private static DiseaseDefCache _diseaseDefCache=new DiseaseDefCache();

		public static int GetCount(bool isShort=false) {
			return _diseaseDefCache.GetCount(isShort);
		}

		public static List<DiseaseDef> GetDeepCopy(bool isShort=false) {
			return _diseaseDefCache.GetDeepCopy(isShort);
		}

		public static List<DiseaseDef> GetWhere(Predicate<DiseaseDef> match,bool isShort=false) {
			return _diseaseDefCache.GetWhere(match,isShort);
		}

		public static DiseaseDef GetFirstOrDefault(Func<DiseaseDef,bool> match,bool isShort=false) {
			return _diseaseDefCache.GetFirstOrDefault(match,isShort);
		}

		///<summary>Refreshes the cache and returns it as a DataTable. This will refresh the ClientWeb's cache and the ServerWeb's cache.</summary>
		public static DataTable RefreshCache() {
			return GetTableFromCache(true);
		}

		///<summary>Fills the local cache with the passed in DataTable.</summary>
		public static void FillCacheFromTable(DataTable table) {
			_diseaseDefCache.FillCacheFromTable(table);
		}

		///<summary>Always refreshes the ClientWeb's cache.</summary>
		public static DataTable GetTableFromCache(bool doRefreshCache) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				DataTable table=Meth.GetTable(MethodBase.GetCurrentMethod(),doRefreshCache);
				_diseaseDefCache.FillCacheFromTable(table);
				return table;
			}
			return _diseaseDefCache.GetTableFromCache(doRefreshCache);
		}

		public static void ClearCache() {
			_diseaseDefCache.ClearCache();
		}
		#endregion

		///<summary>Gets a DiseaseDef from the database. Returns null if not found.</summary>
		public static DiseaseDef GetOne(long diseaseDefNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<DiseaseDef>(MethodBase.GetCurrentMethod(),diseaseDefNum);
			}
			string command="SELECT * FROM diseasedef WHERE DiseaseDefNum="+POut.Long(diseaseDefNum);
			return Crud.DiseaseDefCrud.SelectOne(command);
		}

		///<summary>Gets all DiseaseDefs from the database. Returns empty list if not found.</summary>
		public static List<DiseaseDef> GetDiseaseDefsForApi(int limit,int offset) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DiseaseDef>>(MethodBase.GetCurrentMethod(),limit,offset);
			}
			string command="SELECT * FROM diseasedef ";
			command+="ORDER BY DiseaseDefNum "//same fixed order each time
				+"LIMIT "+POut.Int(offset)+", "+POut.Int(limit);
			return Crud.DiseaseDefCrud.SelectMany(command);
		}

		///<summary>Fixes item orders in DB if needed. Returns true if changes were made.</summary>
		public static bool FixItemOrders() {
			Meth.NoCheckMiddleTierRole();
			bool isChanged=false;
			List<DiseaseDef> listDiseaseDefs=GetDeepCopy();
			listDiseaseDefs.Sort(DiseaseDefs.SortItemOrder);
			for(int i=0;i<listDiseaseDefs.Count;i++) {
				if(listDiseaseDefs[i].ItemOrder==i) {
					continue;
				}
				listDiseaseDefs[i].ItemOrder=i;
				DiseaseDefs.Update(listDiseaseDefs[i]);
				isChanged=true;
			}
			if(isChanged) {
				DiseaseDefs.RefreshCache();
			}
			return isChanged;
		}

		///<summary></summary>
		public static void Update(DiseaseDef diseaseDef) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),diseaseDef);
				return;
			}
			Crud.DiseaseDefCrud.Update(diseaseDef);
		}

		///<summary></summary>
		public static bool Update(DiseaseDef diseaseDef,DiseaseDef diseaseDefOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),diseaseDef,diseaseDefOld);
			}
			return Crud.DiseaseDefCrud.Update(diseaseDef,diseaseDefOld);
		}

		///<summary></summary>
		public static long Insert(DiseaseDef diseaseDef) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				diseaseDef.DiseaseDefNum=Meth.GetLong(MethodBase.GetCurrentMethod(),diseaseDef);
				return diseaseDef.DiseaseDefNum;
			}
			long retVal=Crud.DiseaseDefCrud.Insert(diseaseDef);
			return retVal;
		}

		///<summary>Returns a list of diseasedefs that are in use, and should not be deleted.</summary>
		public static List<long> ValidateDeleteList() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod());
			}
			//Get non-hidden diseasedefnums
			List<long> listDiseaseDefNums=new List<long>();
			string command = "SELECT DiseaseDefNum FROM diseasedef";
			try {
				listDiseaseDefNums.AddRange(Db.GetListLong(command));
			}
			catch {
				//Do Nothing
			}
			List<long> listDiseaseDefNumsNotDeletable=new List<long>();
			if(listDiseaseDefNums==null || listDiseaseDefNums.Count < 1) {
				return listDiseaseDefNumsNotDeletable;
			}
			//In use by preference
			if(listDiseaseDefNums.Contains(PrefC.GetLong(PrefName.ProblemsIndicateNone))) {
				listDiseaseDefNumsNotDeletable.Add(PrefC.GetLong(PrefName.ProblemsIndicateNone));
			}
			//Validate patient attached
			command = "SELECT diseasedef.DiseaseDefNum "
				+"FROM diseasedef "
				+"WHERE EXISTS(SELECT 1 FROM patient "
				+"INNER JOIN disease ON patient.PatNum=disease.PatNum "
				+"WHERE disease.DiseaseDefNum=diseasedef.DiseaseDefNum)";
			try {
				listDiseaseDefNumsNotDeletable.AddRange(Db.GetListLong(command));
			}
			catch {
				//Do Nothing
			}
			//Validate edu resource attached
			command="SELECT diseasedef.DiseaseDefNum "
				+"FROM diseasedef "
				+"WHERE EXISTS(SELECT 1 FROM eduresource "
				+"WHERE eduresource.DiseaseDefNum=diseasedef.DiseaseDefNum)";
			try {
				listDiseaseDefNumsNotDeletable.AddRange(Db.GetListLong(command));
			}
			catch {
				//Do Nothing
			}
			//Validate family health history attached
			command="SELECT diseasedef.DiseaseDefNum "
				+"FROM diseasedef "
				+"WHERE EXISTS(SELECT 1 FROM patient "
				+"INNER JOIN familyhealth ON patient.PatNum=familyhealth.PatNum "
				+"WHERE familyhealth.DiseaseDefNum=diseasedef.DiseaseDefNum)";
			try {
				listDiseaseDefNumsNotDeletable.AddRange(Db.GetListLong(command));
			}
			catch {
				//Do Nothing
			}
			return listDiseaseDefNumsNotDeletable;
		}

		///<summary>Returns a bool indicating if the passed in diseasedefnum is currently in use.</summary>
		public static bool IsDiseaseDefInUse(long diseaseDefNum) {
			if(diseaseDefNum==0) {
				return false;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),diseaseDefNum);
			}
			//In use by preference
			if(diseaseDefNum==PrefC.GetLong(PrefName.ProblemsIndicateNone)) {
				return true;
			}
			//Validate patient attached
			string command="SELECT EXISTS (SELECT * FROM disease WHERE DiseaseDefNum="+POut.Long(diseaseDefNum)+")";
			if(Db.GetScalar(command)=="1") {
				return true;
			}
			//Validate edu resource attached
			command="SELECT EXISTS (SELECT * FROM eduresource WHERE DiseaseDefNum="+POut.Long(diseaseDefNum)+")";
			if(Db.GetScalar(command)=="1") {
				return true;
			}
			//Validate family health history attached
			command="SELECT EXISTS (SELECT * FROM familyhealth WHERE DiseaseDefNum="+POut.Long(diseaseDefNum)+")";
			if(Db.GetScalar(command)=="1") {
				return true;
			}
			return false;
		}

		///<summary>Returns the name of the disease, whether hidden or not.</summary>
		public static string GetName(long diseaseDefNum) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.DiseaseDefNum==diseaseDefNum);
			if(diseaseDef==null) {
				return "";
			}
			return diseaseDef.DiseaseName;
		}

		///<summary>Returns the name of the disease based on SNOMEDCode, then if no match tries ICD9Code, then if no match returns empty string. Used in EHR Patient Lists.</summary>
		public static string GetNameByCode(string SNOMEDorICD9Code) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.SnomedCode==SNOMEDorICD9Code);
			if(diseaseDef!=null) {
				return diseaseDef.DiseaseName;
			}
			diseaseDef=GetFirstOrDefault(x => x.ICD9Code==SNOMEDorICD9Code);
			if(diseaseDef!=null) {
				return diseaseDef.DiseaseName;
			}
			return "";
		}

		///<summary>Returns the DiseaseDefNum based on SNOMEDCode, then if no match tries ICD9Code, then if no match tries ICD10Code, then if no match returns 0. Used in EHR Patient Lists and when automatically inserting pregnancy Dx from FormVitalsignEdit2014.  Will match hidden diseases.</summary>
		public static long GetNumFromCode(string codeValue) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.SnomedCode==codeValue);
			if(diseaseDef!=null) {
				return diseaseDef.DiseaseDefNum;
			}
			diseaseDef=GetFirstOrDefault(x => x.ICD9Code==codeValue);
			if(diseaseDef!=null) {
				return diseaseDef.DiseaseDefNum;
			}
			diseaseDef=GetFirstOrDefault(x => x.Icd10Code==codeValue);
			if(diseaseDef!=null) {
				return diseaseDef.DiseaseDefNum;
			}
			return 0;
		}

		///<summary>Returns the DiseaseDefNum based on SNOMEDCode.  If no match or if SnomedCode is an empty string returns 0.  Only matches SNOMEDCode, not ICD9 or ICD10.</summary>
		public static long GetNumFromSnomed(string snomedCode) {
			Meth.NoCheckMiddleTierRole();
			if(snomedCode=="") {
				return 0;
			}
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.SnomedCode==snomedCode);
			if(diseaseDef==null){
				return 0;
			}
			return diseaseDef.DiseaseDefNum;
		}

		///<summary>Returns the diseaseDef with the specified num.  Will match hidden diseasedefs.</summary>
		public static DiseaseDef GetItem(long diseaseDefNum) {
			Meth.NoCheckMiddleTierRole();
			return GetFirstOrDefault(x => x.DiseaseDefNum==diseaseDefNum);
		}

		///<summary>Returns the diseaseDefNum that exactly matches the specified string.  Used in import functions when you only have the name to work with.  Can return 0 if no match.  Does not match hidden diseases.</summary>
		public static long GetNumFromName(string diseaseName){
			Meth.NoCheckMiddleTierRole();
			return GetNumFromName(diseaseName,false);
		}

		///<summary>Returns the diseaseDefNum that exactly matches the specified string.  Will return 0 if no match.
		///Set matchHidden to true to match hidden diseasedefs as well.</summary>
		public static long GetNumFromName(string diseaseName,bool matchHidden) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=_diseaseDefCache.GetFirstOrDefault(x => x.DiseaseName==diseaseName,!matchHidden);
			if(diseaseDef==null){
				return 0;
			}
			return diseaseDef.DiseaseDefNum;
		}

		///<summary>Used by API. Returns a single diseaseDef with DiseaseName exactly matching the supplied string, or null if no match found. </summary>
		public static DiseaseDef GetDiseaseDefByName(string diseaseName) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<DiseaseDef>(MethodBase.GetCurrentMethod(),diseaseName);
			}
			string command="SELECT * FROM diseasedef WHERE DiseaseName LIKE '"+POut.String(diseaseName)+"'";//matches regardless of case 
			return Crud.DiseaseDefCrud.SelectOne(command);
		}

		///<summary>Returns the diseasedef that has a name exactly matching the specified string. Returns null if no match.  Does not match hidden diseases.</summary>
		public static DiseaseDef GetFromName(string diseaseName) {
			Meth.NoCheckMiddleTierRole();
			return _diseaseDefCache.GetFirstOrDefault(x => x.DiseaseName==diseaseName,true);
		}

		///<summary></summary>
		public static List<long> GetChangedSinceDiseaseDefNums(DateTime dateT) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<long>>(MethodBase.GetCurrentMethod(),dateT);
			}
			string command="SELECT DiseaseDefNum FROM diseasedef WHERE DateTStamp > "+POut.DateT(dateT);
			DataTable table=Db.GetTable(command);
			List<long> listDiseaseDefNums = new List<long>(table.Rows.Count);
			for(int i=0;i<table.Rows.Count;i++) {
				listDiseaseDefNums.Add(PIn.Long(table.Rows[i]["DiseaseDefNum"].ToString()));
			}
			return listDiseaseDefNums;
		}

		///<summary>Used along with GetChangedSinceDiseaseDefNums</summary>
		public static List<DiseaseDef> GetMultDiseaseDefs(List<long> listDiseaseDefNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DiseaseDef>>(MethodBase.GetCurrentMethod(),listDiseaseDefNums);
			}
			string strDiseaseDefNums="";
			DataTable table;
			if(listDiseaseDefNums.Count>0) {
				for(int i=0;i<listDiseaseDefNums.Count;i++) {
					if(i>0) {
						strDiseaseDefNums+="OR ";
					}
					strDiseaseDefNums+="DiseaseDefNum='"+listDiseaseDefNums[i].ToString()+"' ";
				}
				string command="SELECT * FROM diseasedef WHERE "+strDiseaseDefNums;
				table=Db.GetTable(command);
			}
			else {
				table=new DataTable();
			}
			List<DiseaseDef> listDiseaseDefs=Crud.DiseaseDefCrud.TableToList(table);
			return listDiseaseDefs;
		}

		///<summary>Returns true if there exists a disease def containing the SNOMED code passed in, excluding the disease def specified by diseaseDefNum.</summary>
		public static bool ContainsSnomed(string snomedCode, long diseaseDefNum) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.SnomedCode==snomedCode && x.DiseaseDefNum!=diseaseDefNum);
			return (diseaseDef!=null);
		}

		///<summary>Returns true if there exists a disease def containing the ICD9 code passed in, excluding the disease def specified by diseaseDefNum.</summary>
		public static bool ContainsICD9(string icd9Code,long diseaseDefNum) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.ICD9Code==icd9Code && x.DiseaseDefNum!=diseaseDefNum);
			return (diseaseDef!=null);
		}

		///<summary>Returns true if there exists a disease def containing the ICD10 code passed in, excluding the disease def specified by diseaseDefNum.</summary>
		public static bool ContainsIcd10(string icd10Code,long diseaseDefNum) {
			Meth.NoCheckMiddleTierRole();
			DiseaseDef diseaseDef=GetFirstOrDefault(x => x.Icd10Code==icd10Code && x.DiseaseDefNum!=diseaseDefNum);
			return (diseaseDef!=null);
		}

		///<summary>Sync pattern, must sync entire table. Probably only to be used in the master problem list window.</summary>
		public static void Sync(List<DiseaseDef> listDiseaseDefs,List<DiseaseDef> listDiseaseDefsOld) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listDiseaseDefs,listDiseaseDefsOld);
				return;
			}
			Crud.DiseaseDefCrud.Sync(listDiseaseDefs,listDiseaseDefsOld);
		}

		///<summary>Get all diseasedefs that have a pregnancy code that applies to the three CQM measures with pregnancy as an exclusion condition.</summary>
		public static List<DiseaseDef> GetAllPregDiseaseDefs() {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<DiseaseDef>>(MethodBase.GetCurrentMethod());
			}
			Dictionary<string,string> dictionaryEhrCodesPregCQMs=EhrCodes.GetCodesExistingInAllSets(new List<string> { "2.16.840.1.113883.3.600.1.1623","2.16.840.1.113883.3.526.3.378" });
			List<DiseaseDef> listDiseaseDefsToReturn=new List<DiseaseDef>();
			List<DiseaseDef> listDiseaseDefs=GetDeepCopy();
			for(int i=0;i<listDiseaseDefs.Count;i++) {
				if(dictionaryEhrCodesPregCQMs.ContainsKey(listDiseaseDefs[i].ICD9Code) && dictionaryEhrCodesPregCQMs[listDiseaseDefs[i].ICD9Code]=="ICD9CM") {
					listDiseaseDefsToReturn.Add(listDiseaseDefs[i]);
					continue;
				}
				if(dictionaryEhrCodesPregCQMs.ContainsKey(listDiseaseDefs[i].Icd10Code) && dictionaryEhrCodesPregCQMs[listDiseaseDefs[i].Icd10Code]=="ICD10CM") {
					listDiseaseDefsToReturn.Add(listDiseaseDefs[i]);
					continue;
				}
				if(dictionaryEhrCodesPregCQMs.ContainsKey(listDiseaseDefs[i].SnomedCode) && dictionaryEhrCodesPregCQMs[listDiseaseDefs[i].SnomedCode]=="SNOMEDCT") {
					listDiseaseDefsToReturn.Add(listDiseaseDefs[i]);
					continue;
				}
			}
			return listDiseaseDefsToReturn;
		}

		///<summary>Sorts alphabetically by DiseaseName, then by PK.</summary>
		public static int SortAlphabetically(DiseaseDef diseaseDef,DiseaseDef diseaseDefOther) {
			Meth.NoCheckMiddleTierRole();
			if(diseaseDef.DiseaseName!=diseaseDefOther.DiseaseName) {
				return diseaseDef.DiseaseName.CompareTo(diseaseDefOther.DiseaseName);
			}
			return diseaseDef.DiseaseDefNum.CompareTo(diseaseDefOther.DiseaseDefNum);
		}

		public static int SortItemOrder(DiseaseDef diseaseDef,DiseaseDef diseaseDefOther) {
			Meth.NoCheckMiddleTierRole();
			if(diseaseDef.ItemOrder!=diseaseDefOther.ItemOrder) {
				return diseaseDef.ItemOrder.CompareTo(diseaseDefOther.ItemOrder);
			}
			return diseaseDef.DiseaseDefNum.CompareTo(diseaseDefOther.DiseaseDefNum);
		}

		///<summary>Returns DiseaseDefNum for passed in pregnancy code. If pregnacy code doesn't have associated DiseaseDef, creates DiseaseDef.</summary>
		public static long GetDefNumForDefaultPreg(string pregnancyCode) {
			Meth.NoCheckMiddleTierRole();
			long diseaseDefNum=DiseaseDefs.GetNumFromCode(pregnancyCode);//see if the code is attached to a valid diseasedef
			if(diseaseDefNum!=0){
				return diseaseDefNum;
			}
			//no diseasedef in db for the default code, create and insert def
			DiseaseDef diseaseDef=new DiseaseDef();
			diseaseDef.DiseaseName="Pregnant";
			string pregnancyCodeSys=PrefC.GetString(PrefName.PregnancyDefaultCodeSystem);
			switch(pregnancyCodeSys) {
				case "ICD9CM":
					diseaseDef.ICD9Code=pregnancyCode;
					break;
				case "ICD10CM":
					diseaseDef.Icd10Code=pregnancyCode;
					break;
				case "SNOMEDCT":
					diseaseDef.SnomedCode=pregnancyCode;
					break;
			}
			diseaseDefNum=DiseaseDefs.Insert(diseaseDef);
			DiseaseDefs.RefreshCache();
			Signalods.SetInvalid(InvalidType.Diseases);
			SecurityLogs.MakeLogEntry(EnumPermType.ProblemDefEdit,0,diseaseDef.DiseaseName+" added.");
			return diseaseDefNum;
		}

		#region 26.1: Problem merge (v26_1-lists-problem-merge-keep)
		///<summary>The number of patients who have at least one problem with this definition.</summary>
		public static int GetPatientCount(long diseaseDefNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),diseaseDefNum);
			}
			string command="SELECT COUNT(DISTINCT PatNum) FROM disease WHERE DiseaseDefNum="+POut.Long(diseaseDefNum);
			return PIn.Int(Db.GetCount(command));
		}

		///<summary>The text of one choice in the Merge Problems window: the name, the ICD-10 and SNOMED CT codes that are set, whether the problem is
		///hidden, and how many patients have it.</summary>
		public static string GetMergeChoiceText(DiseaseDef diseaseDef,int countPatients) {
			Meth.NoCheckMiddleTierRole();
			string text=diseaseDef.DiseaseName??"";
			List<string> listCodes=new List<string>();
			if(!string.IsNullOrWhiteSpace(diseaseDef.Icd10Code)) {
				listCodes.Add(Lans.g("DiseaseDefs","ICD-10")+" "+diseaseDef.Icd10Code.Trim());
			}
			if(!string.IsNullOrWhiteSpace(diseaseDef.SnomedCode)) {
				listCodes.Add(Lans.g("DiseaseDefs","SNOMED CT")+" "+diseaseDef.SnomedCode.Trim());
			}
			if(listCodes.Count>0) {
				text+=" ("+string.Join(", ",listCodes)+")";
			}
			if(diseaseDef.IsHidden) {
				text+=" ["+Lans.g("DiseaseDefs","hidden")+"]";
			}
			if(countPatients==1) {
				text+=", "+Lans.g("DiseaseDefs","used by 1 patient");
			}
			else {
				text+=", "+Lans.g("DiseaseDefs","used by")+" "+countPatients+" "+Lans.g("DiseaseDefs","patients");
			}
			return text;
		}

		///<summary>Merges two problem definitions. Every reference to diseaseDefNumDiscard moves to diseaseDefNumKeep, then the discarded definition
		///is deleted. The kept definition itself is not changed: its name, codes and hidden flag stay as they are, and nothing of the discarded one is
		///copied into it. References moved: patient problems (disease), Rx alerts, EHR education resources, family health history, CDS triggers
		///(ehrtrigger.ProblemDefNumList), problem reminder rules and the ProblemsIndicateNone preference. A patient who had both problems keeps both
		///rows; the result counts those patients. The tables are MyISAM, so there is no transaction: the definition is deleted last, and if a step
		///fails the discarded definition still exists, so running the same merge again finishes it. The caller makes the ProblemDefEdit security log
		///entry (see GetMergeLogText) and refreshes the problem cache, and the preference cache when IsProblemsIndicateNoneMoved is true. Throws an
		///ApplicationException when the two numbers are the same or either definition no longer exists.</summary>
		public static DiseaseDefMergeResult Merge(long diseaseDefNumKeep,long diseaseDefNumDiscard) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<DiseaseDefMergeResult>(MethodBase.GetCurrentMethod(),diseaseDefNumKeep,diseaseDefNumDiscard);
			}
			if(diseaseDefNumKeep==diseaseDefNumDiscard) {
				throw new ApplicationException(Lans.g("DiseaseDefs","Choose two different problems to merge."));
			}
			DiseaseDef diseaseDefKeep=GetOne(diseaseDefNumKeep);
			DiseaseDef diseaseDefDiscard=GetOne(diseaseDefNumDiscard);
			if(diseaseDefKeep==null || diseaseDefDiscard==null) {
				throw new ApplicationException(Lans.g("DiseaseDefs","One of the problems no longer exists. Open the Problems window again and retry."));
			}
			DiseaseDefMergeResult result=new DiseaseDefMergeResult();
			result.DiseaseDefNumKept=diseaseDefKeep.DiseaseDefNum;
			result.DiseaseNameKept=diseaseDefKeep.DiseaseName;
			result.DiseaseDefNumDiscarded=diseaseDefDiscard.DiseaseDefNum;
			result.DiseaseNameDiscarded=diseaseDefDiscard.DiseaseName;
			string keep=POut.Long(diseaseDefNumKeep);
			string discard=POut.Long(diseaseDefNumDiscard);
			//Counted before anything moves: afterwards the two rows can't be told apart.
			string command="SELECT COUNT(DISTINCT disease.PatNum) FROM disease "
				+"WHERE disease.DiseaseDefNum="+discard+" "
				+"AND EXISTS(SELECT 1 FROM disease diseaseKept WHERE diseaseKept.PatNum=disease.PatNum AND diseaseKept.DiseaseDefNum="+keep+")";
			result.CountPatientsWithBoth=PIn.Int(Db.GetCount(command));
			result.CountProblemsMoved=Db.NonQ("UPDATE disease SET DiseaseDefNum="+keep+" WHERE DiseaseDefNum="+discard);
			result.CountRxAlertsMoved=Db.NonQ("UPDATE rxalert SET DiseaseDefNum="+keep+" WHERE DiseaseDefNum="+discard);
			result.CountEduResourcesMoved=Db.NonQ("UPDATE eduresource SET DiseaseDefNum="+keep+" WHERE DiseaseDefNum="+discard);
			result.CountFamilyHealthMoved=Db.NonQ("UPDATE familyhealth SET DiseaseDefNum="+keep+" WHERE DiseaseDefNum="+discard);
			result.CountReminderRulesMoved=Db.NonQ("UPDATE reminderrule SET CriterionFK="+keep+" "
				+"WHERE ReminderCriterion="+POut.Int((int)EhrCriterion.Problem)+" AND CriterionFK="+discard);
			//CDS triggers keep problem numbers in a text column, each number wrapped in spaces: " 12  34 ".
			command="SELECT EhrTriggerNum,ProblemDefNumList FROM ehrtrigger WHERE ProblemDefNumList LIKE '% "+discard+" %'";
			DataTable table=Db.GetTable(command);
			for(int i=0;i<table.Rows.Count;i++) {
				string problemDefNumList=table.Rows[i]["ProblemDefNumList"].ToString();
				string problemDefNumListNew=ReplaceNumInSpacedList(problemDefNumList,diseaseDefNumDiscard,diseaseDefNumKeep);
				if(problemDefNumListNew==problemDefNumList) {
					continue;
				}
				Db.NonQ("UPDATE ehrtrigger SET ProblemDefNumList='"+POut.String(problemDefNumListNew)+"' "
					+"WHERE EhrTriggerNum="+POut.Long(PIn.Long(table.Rows[i]["EhrTriggerNum"].ToString())));
				result.CountCdsTriggersMoved++;
			}
			if(PrefC.GetLong(PrefName.ProblemsIndicateNone)==diseaseDefNumDiscard) {
				Prefs.UpdateLong(PrefName.ProblemsIndicateNone,diseaseDefNumKeep);
				result.IsProblemsIndicateNoneMoved=true;
			}
			//Sheet and eForm definitions name problems ("problem:Name"), not numbers. They are left alone, but counted so the office can edit them.
			if(diseaseDefDiscard.DiseaseName!=diseaseDefKeep.DiseaseName) {
				string fieldName=POut.String("problem:"+diseaseDefDiscard.DiseaseName);
				result.CountSheetFieldDefsNamingDiscarded=PIn.Int(Db.GetCount("SELECT COUNT(*) FROM sheetfielddef WHERE BINARY FieldName='"+fieldName+"'"));
				result.CountEFormFieldDefsNamingDiscarded=PIn.Int(Db.GetCount("SELECT COUNT(*) FROM eformfielddef WHERE BINARY DbLink='"+fieldName+"'"));
			}
			Db.NonQ("DELETE FROM diseasedef WHERE DiseaseDefNum="+discard);
			return result;
		}

		///<summary>Replaces one number in a list whose numbers are each wrapped in spaces, like ehrtrigger.ProblemDefNumList (" 12  34 "). Each number
		///is kept once, in its first place. Returns the list unchanged when numOld is not in it.</summary>
		public static string ReplaceNumInSpacedList(string numList,long numOld,long numNew) {
			Meth.NoCheckMiddleTierRole();
			if(string.IsNullOrEmpty(numList)) {
				return numList;
			}
			List<string> listNums=numList.Split(new char[] { ' ' },StringSplitOptions.RemoveEmptyEntries).ToList();
			string strOld=numOld.ToString();
			if(!listNums.Contains(strOld)) {
				return numList;
			}
			string strNew=numNew.ToString();
			List<string> listNumsNew=new List<string>();
			for(int i=0;i<listNums.Count;i++) {
				string num=(listNums[i]==strOld ? strNew : listNums[i]);
				if(!listNumsNew.Contains(num)) {
					listNumsNew.Add(num);
				}
			}
			return string.Concat(listNumsNew.Select(x => " "+x+" "));
		}

		///<summary>The ProblemDefEdit security log text for a merge: "Problem X merged into Y.", plus the number of patient problems moved.</summary>
		public static string GetMergeLogText(DiseaseDefMergeResult result) {
			Meth.NoCheckMiddleTierRole();
			return Lans.g("DiseaseDefs","Problem")+" \""+result.DiseaseNameDiscarded+"\" (#"+result.DiseaseDefNumDiscarded+") "
				+Lans.g("DiseaseDefs","merged into")+" \""+result.DiseaseNameKept+"\" (#"+result.DiseaseDefNumKept+"). "
				+Lans.g("DiseaseDefs","Patient problems moved")+": "+result.CountProblemsMoved+".";
		}

		///<summary>The message shown after a merge: what moved, and the follow-ups for the office: patients who now have the problem twice, and
		///sheet or eForm fields that still name the discarded problem.</summary>
		public static string GetMergeSummaryText(DiseaseDefMergeResult result) {
			Meth.NoCheckMiddleTierRole();
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.AppendLine(Lans.g("DiseaseDefs","Problem")+" \""+result.DiseaseNameDiscarded+"\" "+Lans.g("DiseaseDefs","was merged into")+" \""
				+result.DiseaseNameKept+"\".");
			stringBuilder.AppendLine(Lans.g("DiseaseDefs","Patient problems moved")+": "+result.CountProblemsMoved);
			long countOther=result.CountRxAlertsMoved+result.CountEduResourcesMoved+result.CountFamilyHealthMoved+result.CountReminderRulesMoved
				+result.CountCdsTriggersMoved;
			if(countOther>0) {
				stringBuilder.AppendLine(Lans.g("DiseaseDefs","Rx alerts, education resources, family health entries, reminder rules and CDS triggers moved")
					+": "+countOther);
			}
			if(result.IsProblemsIndicateNoneMoved) {
				stringBuilder.AppendLine(Lans.g("DiseaseDefs","The problem that indicates the patient has no problems is now")+" \""+result.DiseaseNameKept+"\".");
			}
			if(result.CountPatientsWithBoth>0) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine(Lans.g("DiseaseDefs","Patients who had both problems now have this problem twice. Both entries were kept")+": "
					+result.CountPatientsWithBoth);
			}
			int countFormFields=result.CountSheetFieldDefsNamingDiscarded+result.CountEFormFieldDefsNamingDiscarded;
			if(countFormFields>0) {
				stringBuilder.AppendLine();
				stringBuilder.AppendLine(Lans.g("DiseaseDefs","Sheet and eForm fields that still refer to the old problem name and need to be edited")+": "
					+countFormFields);
			}
			return stringBuilder.ToString().TrimEnd();
		}
		#endregion 26.1: Problem merge

	/*public static DiseaseDef GetByICD9Code(string ICD9Code) {///<summary>Returns the diseasedef that has a name exactly matching the specified string. Returns null if no match.  Does not match hidden diseases.</summary>
			Meth.NoCheckMiddleTierRole();
			for(int i=0;i<List.Length;i++) {
				if(ICD9Code==List[i].ICD9Code) {
					return List[i];
				}
			}
			return null;
		}*/
	}

	///<summary>What DiseaseDefs.Merge did. Counts are rows changed.</summary>
	[Serializable]
	public class DiseaseDefMergeResult {
		public long DiseaseDefNumKept;
		public string DiseaseNameKept;
		public long DiseaseDefNumDiscarded;
		public string DiseaseNameDiscarded;
		///<summary>Patient problems (disease rows) moved from the discarded definition to the kept one.</summary>
		public long CountProblemsMoved;
		///<summary>Patients who had both problems before the merge and now have the kept problem twice. Both rows are kept.</summary>
		public int CountPatientsWithBoth;
		public long CountRxAlertsMoved;
		public long CountEduResourcesMoved;
		public long CountFamilyHealthMoved;
		public long CountReminderRulesMoved;
		public long CountCdsTriggersMoved;
		///<summary>True when the ProblemsIndicateNone preference pointed to the discarded definition and now points to the kept one.</summary>
		public bool IsProblemsIndicateNoneMoved;
		///<summary>Sheet field definitions named "problem:" plus the discarded name. They are not changed.</summary>
		public int CountSheetFieldDefsNamingDiscarded;
		///<summary>eForm field definitions whose DbLink is "problem:" plus the discarded name. They are not changed.</summary>
		public int CountEFormFieldDefsNamingDiscarded;
	}

		



		
	

	

	


}










