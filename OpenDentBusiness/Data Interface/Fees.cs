using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading;
using CodeBase;

namespace OpenDentBusiness{
	///<summary>Fees for procedure codes, by fee schedule, clinic and provider. Since Open Dental 24.4 a fee row also has a start date (fee.DateEffective):
	///several rows can exist for one fee schedule, code, clinic and provider, and a lookup picks the row in effect on an as-of date. See the region
	///"Effective dates" below for the rules.</summary>
	public class Fees {
		#region Effective dates
		///<summary>The DateEffective of a fee that has no start date: 0001-01-01, the column default. Such a fee is always in effect.</summary>
		public static readonly DateTime DateEffectiveAlways=DateTime.MinValue;
		///<summary>The as-of date set by ScopeDateAsOf for the code running on this thread. MinValue when no scope is open.</summary>
		[ThreadStatic]
		private static DateTime _dateAsOfScope;

		///<summary>Returns the as-of date a fee lookup uses. A real date passed in wins. Otherwise the date of the innermost ScopeDateAsOf open on this
		///thread, otherwise today. Screens without a date filter, charting and treatment planning all end up with today.</summary>
		public static DateTime GetDateAsOf(DateTime dateAsOf=default(DateTime)) {
			Meth.NoCheckMiddleTierRole();
			if(dateAsOf.Year>1880) {
				return dateAsOf.Date;
			}
			if(_dateAsOfScope.Year>1880) {
				return _dateAsOfScope;
			}
			return DateTime.Today;
		}

		///<summary>Until the returned object is disposed, fee lookups on this thread that are not given a date use dateAsOf. Used around insurance
		///estimates so that code which looks up fees deep inside them (allowed fees, blue book, fixed benefits) uses the procedure's date without every
		///method passing it along. A date before 1880 means "no override". Scopes nest; disposing restores the previous date.</summary>
		public static IDisposable ScopeDateAsOf(DateTime dateAsOf) {
			Meth.NoCheckMiddleTierRole();
			return new DateAsOfScope(dateAsOf);
		}

		///<summary>The as-of date for fees of one procedure. A completed procedure uses its procedure date (when it isn't in the future), so setting a
		///backdated procedure complete, or estimating it later, uses the fees that were in effect when the work was done. Everything else (treatment
		///planned, existing, and so on) uses today.</summary>
		public static DateTime GetDateAsOfForProc(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			if(procedure!=null && procedure.ProcStatus==ProcStat.C && procedure.ProcDate.Year>1880 && procedure.ProcDate.Date<=DateTime.Today) {
				return procedure.ProcDate.Date;
			}
			return DateTime.Today;
		}

		///<summary>True if the fee's start date is on or before the as-of date.</summary>
		public static bool IsInEffect(Fee fee,DateTime dateAsOf) {
			Meth.NoCheckMiddleTierRole();
			return fee.DateEffective.Date<=dateAsOf.Date;
		}

		///<summary>Text for security logs and messages: ", effective 2026-01-01", or "" for a fee that is always in effect.</summary>
		public static string GetDateEffectiveLogText(DateTime dateEffective) {
			Meth.NoCheckMiddleTierRole();
			if(dateEffective.Year<1880) {
				return "";
			}
			return ", "+Lans.g("Fees","effective")+" "+dateEffective.ToString("yyyy-MM-dd");
		}

		///<summary>The date-aware lookup every fee lookup ends in. For the given code and schedule it picks, at each override level, the row with the
		///latest DateEffective on or before dateAsOf, then applies the usual override order: provider and clinic, provider only (clinic 0), clinic
		///only (provider 0), then the schedule's default (clinic 0, provider 0). For a global schedule clinicNum and provNum are ignored. With
		///doGetExactMatch, only the exact clinic and provider level is searched. Returns null if nothing matches. Rows with the same key and date are
		///not allowed, but if there are some, the first one in the list wins. No database or cache access, so it can be unit tested.</summary>
		public static Fee GetFeeFromList(List<Fee> listFees,long codeNum,long feeSchedNum,long clinicNum,long provNum,bool isGlobal,DateTime dateAsOf,
			bool doGetExactMatch=false)
		{
			Meth.NoCheckMiddleTierRole();
			if(listFees==null) {
				return null;
			}
			if(isGlobal && !doGetExactMatch) {
				clinicNum=0;
				provNum=0;
			}
			dateAsOf=dateAsOf.Date;
			//0: exact clinic and provider. 1: provider override (clinic 0). 2: clinic override (provider 0). 3: default (clinic 0, provider 0).
			Fee[] arrayFeesBest=new Fee[4];
			for(int i=0;i<listFees.Count;i++) {
				Fee fee=listFees[i];
				if(fee==null || fee.CodeNum!=codeNum || fee.FeeSched!=feeSchedNum) {
					continue;
				}
				if(fee.DateEffective.Date>dateAsOf) {
					continue;//starts later
				}
				int level;
				if(fee.ClinicNum==clinicNum && fee.ProvNum==provNum) {
					level=0;
				}
				else if(fee.ClinicNum==0 && fee.ProvNum==provNum) {
					level=1;
				}
				else if(fee.ClinicNum==clinicNum && fee.ProvNum==0) {
					level=2;
				}
				else if(fee.ClinicNum==0 && fee.ProvNum==0) {
					level=3;
				}
				else {
					continue;
				}
				if(arrayFeesBest[level]==null || fee.DateEffective>arrayFeesBest[level].DateEffective) {
					arrayFeesBest[level]=fee;
				}
			}
			if(arrayFeesBest[0]!=null) {
				return arrayFeesBest[0];
			}
			if(doGetExactMatch || isGlobal) {
				return null;
			}
			return arrayFeesBest[1]??arrayFeesBest[2]??arrayFeesBest[3];
		}

		///<summary>Returns the row with exactly this key and start date, or null. Used to decide between updating a dated row and inserting one.</summary>
		public static Fee GetFeeExactDate(List<Fee> listFees,long codeNum,long feeSchedNum,long clinicNum,long provNum,DateTime dateEffective) {
			Meth.NoCheckMiddleTierRole();
			if(listFees==null) {
				return null;
			}
			return listFees.Find(x => x!=null && x.CodeNum==codeNum && x.FeeSched==feeSchedNum && x.ClinicNum==clinicNum && x.ProvNum==provNum
				&& x.DateEffective.Date==dateEffective.Date);
		}

		///<summary>Reduces a list of fee rows to one row per fee schedule, code, clinic and provider: the row in effect on dateAsOf (today if not
		///given). Keys whose rows all start later are left out. The order of the input list is kept. This is what screens without a date filter show.
		///</summary>
		public static List<Fee> GetRowsInEffect(List<Fee> listFees,DateTime dateAsOf=default(DateTime)) {
			Meth.NoCheckMiddleTierRole();
			dateAsOf=GetDateAsOf(dateAsOf);
			Dictionary<FeeRowKey,Fee> dictBest=new Dictionary<FeeRowKey,Fee>();
			for(int i=0;i<listFees.Count;i++) {
				Fee fee=listFees[i];
				if(fee.DateEffective.Date>dateAsOf) {
					continue;
				}
				FeeRowKey feeRowKey=new FeeRowKey(fee);
				if(!dictBest.TryGetValue(feeRowKey,out Fee feeBest) || fee.DateEffective>feeBest.DateEffective) {
					dictBest[feeRowKey]=fee;
				}
			}
			HashSet<Fee> hashSetKeep=new HashSet<Fee>(dictBest.Values);
			return listFees.FindAll(x => hashSetKeep.Contains(x));
		}

		///<summary>Throws an ApplicationException if two of the fees have the same fee schedule, code, clinic, provider and start date.</summary>
		public static void ValidateNoDuplicateDates(List<Fee> listFees) {
			Meth.NoCheckMiddleTierRole();
			HashSet<string> hashSetKeys=new HashSet<string>();
			for(int i=0;i<listFees.Count;i++) {
				string key=GetKeyWithDate(listFees[i]);
				if(!hashSetKeys.Add(key)) {
					throw new ApplicationException(GetDuplicateMessage(listFees[i]));
				}
			}
		}

		///<summary>The duplicate rule for a sync (SynchList). Throws an ApplicationException if a row that the sync would insert or change has the same
		///fee schedule, code, clinic, provider and start date as another row of listFeesNew. Rows that are unchanged from listFeesDb are not checked
		///against each other: databases from before fee effective dates had no unique key on fee, so they can hold exact duplicates (Database
		///Maintenance's FeeDeleteDuplicates removes them), and such rows must not stop the user from saving other fees of the same schedule.</summary>
		public static void ValidateNoDuplicateDatesForSync(List<Fee> listFeesNew,List<Fee> listFeesDb) {
			Meth.NoCheckMiddleTierRole();
			Dictionary<long,Fee> dictFeesDb=new Dictionary<long,Fee>();
			if(listFeesDb!=null) {
				for(int i=0;i<listFeesDb.Count;i++) {
					if(listFeesDb[i].FeeNum!=0) {
						dictFeesDb[listFeesDb[i].FeeNum]=listFeesDb[i];
					}
				}
			}
			Dictionary<string,int> dictCounts=new Dictionary<string,int>();
			for(int i=0;i<listFeesNew.Count;i++) {
				string key=GetKeyWithDate(listFeesNew[i]);
				dictCounts.TryGetValue(key,out int count);
				dictCounts[key]=count+1;
			}
			for(int i=0;i<listFeesNew.Count;i++) {
				Fee fee=listFeesNew[i];
				bool isInsertOrChange=fee.FeeNum==0
					|| !dictFeesDb.TryGetValue(fee.FeeNum,out Fee feeDb)
					|| Crud.FeeCrud.UpdateComparison(fee,feeDb);//includes DateEffective
				if(isInsertOrChange && dictCounts[GetKeyWithDate(fee)]>1) {
					throw new ApplicationException(GetDuplicateMessage(fee));
				}
			}
		}

		private static string GetKeyWithDate(Fee fee) {
			return fee.FeeSched+","+fee.CodeNum+","+fee.ClinicNum+","+fee.ProvNum+","+fee.DateEffective.ToString("yyyy-MM-dd");
		}

		private static string GetDuplicateMessage(Fee fee) {
			string date=(fee.DateEffective.Year<1880 ? Lans.g("Fees","no start date") : fee.DateEffective.ToString("yyyy-MM-dd"));
			return Lans.g("Fees","A fee with the same fee schedule, procedure code, clinic, provider and effective date already exists.")
				+" (FeeSched "+fee.FeeSched+", CodeNum "+fee.CodeNum+", ClinicNum "+fee.ClinicNum+", ProvNum "+fee.ProvNum+", "+date+")";
		}

		///<summary>Key of a fee row without its date.</summary>
		private struct FeeRowKey:IEquatable<FeeRowKey> {
			public readonly long FeeSched;
			public readonly long CodeNum;
			public readonly long ClinicNum;
			public readonly long ProvNum;

			public FeeRowKey(Fee fee) {
				FeeSched=fee.FeeSched;
				CodeNum=fee.CodeNum;
				ClinicNum=fee.ClinicNum;
				ProvNum=fee.ProvNum;
			}

			public bool Equals(FeeRowKey other) {
				return FeeSched==other.FeeSched && CodeNum==other.CodeNum && ClinicNum==other.ClinicNum && ProvNum==other.ProvNum;
			}

			public override bool Equals(object obj) {
				return obj is FeeRowKey && Equals((FeeRowKey)obj);
			}

			public override int GetHashCode() {
				unchecked {
					int hash=FeeSched.GetHashCode();
					hash=hash*31+CodeNum.GetHashCode();
					hash=hash*31+ClinicNum.GetHashCode();
					hash=hash*31+ProvNum.GetHashCode();
					return hash;
				}
			}
		}

		private class DateAsOfScope:IDisposable {
			private readonly DateTime _datePrevious;
			private bool _isDisposed;

			public DateAsOfScope(DateTime dateAsOf) {
				_datePrevious=_dateAsOfScope;
				_dateAsOfScope=(dateAsOf.Year>1880 ? dateAsOf.Date : _datePrevious);
			}

			public void Dispose() {
				if(_isDisposed) {
					return;
				}
				_isDisposed=true;
				_dateAsOfScope=_datePrevious;
			}
		}
		#endregion Effective dates

		#region Get Methods
		///<summary>Gets the list of fees by clinic num from the db.</summary>
		public static List<Fee> GetByClinicNum(long clinicNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),clinicNum);
			}
			string command="SELECT * FROM fee WHERE ClinicNum = "+POut.Long(clinicNum);
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Gets the list of fees by feeschednums and clinicnums from the db.  Returns an empty list if listFeeSchedNums is null or empty.
		///Throws an application exception if listClinicNums is null or empty.  Always provide at least one ClinicNum.
		///We throw instead of returning an empty list which would make it look like there are no fees for the fee schedules passed in.
		///If this method returns an empty list it is because no valied fee schedules were given or the database truly doesn't have any fees.</summary>
		public static List<FeeLim> GetByFeeSchedNumsClinicNums(List<long> listFeeSchedNums,List<long> listClinicNums) {
			if(listFeeSchedNums==null || listFeeSchedNums.Count==0) {
				return new List<FeeLim>();//This won't hurt the FeeCache because there will be no corresponding fee schedules to "blank out".
			}
			if(listClinicNums==null || listClinicNums.Count==0) {
				//Returning an empty list here would be detrimental to the FeeCache.
				throw new ApplicationException("Invalid listClinicNums passed into GetByFeeSchedNumsClinicNums()");
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				//Unusual Middle Tier check. This method can cause out of memory exceptions when called over Middle Tier, so we are batching into multiple
				//calls of 100 fee schedules at a time.
				List<FeeLim> listFeeLims=new List<FeeLim>();
				for(int i=0;i<listFeeSchedNums.Count;i+=100) {
					List<long> listFeeSchedsNumsThisBatch=listFeeSchedNums.GetRange(i,Math.Min(100,listFeeSchedNums.Count-i));
					listFeeLims.AddRange(Meth.GetObject<List<FeeLim>>(MethodBase.GetCurrentMethod(),listFeeSchedsNumsThisBatch,listClinicNums));
				}
				return listFeeLims;
			}
			string command="SELECT FeeNum,Amount,FeeSched,CodeNum,ClinicNum,ProvNum,SecDateTEdit,DateEffective FROM fee "
				+"WHERE FeeSched IN ("+string.Join(",",listFeeSchedNums.Select(x => POut.Long(x)))+") "
				+"AND ClinicNum IN ("+string.Join(",",listClinicNums.Select(x => POut.Long(x)))+")";
			List<FeeLim> listFeeLimsRet=Db.GetTable(command).AsEnumerable()
				.Select(x => new FeeLim {
					FeeNum=PIn.Long(x["FeeNum"].ToString()),
					Amount=PIn.Double(x["Amount"].ToString()),
					FeeSched=PIn.Long(x["FeeSched"].ToString()),
					CodeNum=PIn.Long(x["CodeNum"].ToString()),
					ClinicNum=PIn.Long(x["ClinicNum"].ToString()),
					ProvNum=PIn.Long(x["ProvNum"].ToString()),
					SecDateTEdit=PIn.DateT(x["SecDateTEdit"].ToString()),
					DateEffective=PIn.Date(x["DateEffective"].ToString()),
				}).ToList();
			return listFeeLimsRet;
		}

		///<summary>Counts the number of fees in the db for this fee sched, including all clinic and prov overrides. Dated rows of one code, clinic and
		///provider count once.</summary>
		public static int GetCountByFeeSchedNum(long feeSchedNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetInt(MethodBase.GetCurrentMethod(),feeSchedNum);
			}
			string command="SELECT COUNT(DISTINCT CodeNum,ClinicNum,ProvNum) FROM fee WHERE FeeSched ="+POut.Long(feeSchedNum);
			return PIn.Int(Db.GetCount(command));
		}

		///<summary>Searches for the given codeNum and feeSchedNum and finds the most appropriate match for the clinicNum and provNum, among the rows in
		///effect on dateAsOf (see GetDateAsOf: today unless a date or a ScopeDateAsOf is given). If listFees is null, it will go to db.</summary>
		public static Fee GetFee(long codeNum,long feeSchedNum,long clinicNum,long provNum,List<Fee> listFees=null,DateTime dateAsOf=default(DateTime)) {
			Meth.NoCheckMiddleTierRole();
			dateAsOf=GetDateAsOf(dateAsOf);
			//use listFees if supplied regardless of the FeesUseCache pref since the fee cache is not really thread safe
			if(listFees!=null) {
				return GetFeeFromList(listFees,codeNum,feeSchedNum,clinicNum,provNum,dateAsOf);
			}
			return GetFeeFromDb(codeNum,feeSchedNum,clinicNum,provNum,dateAsOf:dateAsOf);
		}

		///<summary>Searches the db for the fee in effect on dateAsOf (today if not given) for codeNum and feeSchedNum, using the clinic and provider
		///override order. Set doGetExactMatch to true to search only the exact clinicNum and provNum given; then it returns null if no such row is in
		///effect. The goal of the exact match is to have a way to check the database for "duplicate" fees before adding more fees to the db.</summary>
		public static Fee GetFeeFromDb(long codeNum,long feeSchedNum,long clinicNum=0,long provNum=0,bool doGetExactMatch=false,
			DateTime dateAsOf=default(DateTime))
		{
			//Resolve the date on the client: a ScopeDateAsOf only exists on the calling thread.
			dateAsOf=GetDateAsOf(dateAsOf);
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<Fee>(MethodBase.GetCurrentMethod(),codeNum,feeSchedNum,clinicNum,provNum,doGetExactMatch,dateAsOf);
			}
			bool isGlobal=FeeScheds.IsGlobal(feeSchedNum);
			if(isGlobal && !doGetExactMatch) {
				clinicNum=0;
				provNum=0;
			}
			//All rows of every override level that could apply, then the same date-aware rules as for a list.
			string command="SELECT * FROM fee "
				+"WHERE CodeNum="+POut.Long(codeNum)+" "
				+"AND FeeSched="+POut.Long(feeSchedNum)+" "
				+"AND ClinicNum IN(0,"+POut.Long(clinicNum)+") "
				+"AND ProvNum IN(0,"+POut.Long(provNum)+") "
				+"AND DateEffective<="+POut.Date(dateAsOf);
			List<Fee> listFees=Crud.FeeCrud.SelectMany(command);
			return GetFeeFromList(listFees,codeNum,feeSchedNum,clinicNum,provNum,isGlobal,dateAsOf,doGetExactMatch);
		}

		///<summary>Same logic as GetFeeFromDb. Typical to pass in a list of fees for just one or a few feescheds so that the search goes quickly.
		///When doGetExactMatch is true, this will return either the fee that matches the parameters exactly, or null if no such fee exists.
		///When doGetExactMatch is false, and the fee schedule is global, we ignore the clinicNum and provNum and return the HQ fee that matches the given codeNum and feeSchedNum.
		///When doGetExactMatch is false, and the fee schedule is not global, and no exact match exists we attempt to return the closest matching fee in this order:
		///1 - The fee with the same codeNum, feeSchedNum, and providerNum, with a clinicNum of 0
		///2 - The fee with the same codeNum, feeSchedNum, and clinicNum, with a providerNum of 0
		///3 - The fee with the same codeNum, feeSchedNum, and both a clinicNum and providerNum of 0
		///At each level only rows in effect on dateAsOf count, and the latest of them wins. If no partial match can be found, return null.</summary>
		private static Fee GetFeeFromList(List<Fee> listFees,long codeNum,long feeSched,long clinicNum,long provNum,DateTime dateAsOf,
			bool doGetExactMatch=false)
		{
			Meth.NoCheckMiddleTierRole();
			return GetFeeFromList(listFees,codeNum,feeSched,clinicNum,provNum,FeeScheds.IsGlobal(feeSched),dateAsOf,doGetExactMatch);
		}

		public static List<Fee> GetListExactForClinicsFromDb(long codeNum,long feeSchedNum,long provNum,List<long> listClinicNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),codeNum,feeSchedNum,provNum,listClinicNums);
			}
			string command="SELECT fee.* FROM fee "
				+"WHERE fee.CodeNum="+POut.Long(codeNum)+" "
				+"AND fee.FeeSched="+POut.Long(feeSchedNum)+" "
				+"AND fee.ProvNum="+POut.Long(provNum);
			if(!listClinicNums.IsNullOrEmpty()) {
				command+= " AND fee.ClinicNum IN("+string.Join(",",listClinicNums.Select(POut.Long))+")";
			}
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Gets fees for up to three feesched/clinic/prov combos. If filtering with a ClinicNum and/or ProvNum, it only includes fees that match that clinicNum/provnum or have zero.  This reduces the result set if there are clinic or provider overrides. This could easily scale to many thousands of clinics and providers.</summary>
		public static List<Fee> GetListForScheds(long feeSched1,long clinicNum1=0,long provNum1=0,long feeSched2=0,long clinicNum2=0,long provNum2=0,long feeSched3=0,long clinicNum3=0,long provNum3=0){
			Meth.NoCheckMiddleTierRole();
			return GetListForSchedsAndClinics(feeSched1,new List<long> { clinicNum1 },provNum1,feeSched2,new List<long> { clinicNum2 },provNum2,feeSched3,new List<long> { clinicNum3 },provNum3);
		}
		
		///<summary>Gets fees for up to three feesched/clinic/prov combos. If filtering with a ClinicNum and/or ProvNum, it only includes fees that match that clinicNum/provnum or have zero.  This reduces the result set if there are clinic or provider overrides. This could easily scale to many thousands of clinics and providers.</summary>
		public static List<Fee> GetListForSchedsAndClinics(long feeSched1,List<long> listClinics1=null,long provNum1=0,long feeSched2=0,List<long> listClinics2=null,
			long provNum2=0,long feeSched3=0,List<long> listClinics3=null,long provNum3=0)
		{
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),feeSched1,listClinics1,provNum1,feeSched2,listClinics2,provNum2,feeSched3,listClinics3,provNum3);
			}
			string command="SELECT * FROM fee WHERE "
				+"(FeeSched="+POut.Long(feeSched1)+" AND ClinicNum IN(0"+(listClinics1.IsNullOrEmpty()?"":(","+string.Join(",",listClinics1)))+") AND ProvNum IN(0,"+POut.Long(provNum1)+"))";
			if(feeSched2!=0){
				command+=" OR (FeeSched="+POut.Long(feeSched2)+" AND ClinicNum IN(0"+(listClinics2.IsNullOrEmpty()?"":(","+string.Join(",",listClinics2)))+") AND ProvNum IN(0,"+POut.Long(provNum2)+"))";
			}
			if(feeSched3!=0){
				command+=" OR (FeeSched="+POut.Long(feeSched3)+" AND ClinicNum IN(0"+(listClinics3.IsNullOrEmpty()?"":(","+string.Join(",",listClinics3)))+") AND ProvNum IN(0,"+POut.Long(provNum3)+"))";
			}
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Gets all possible fees associated with the various objects passed in.  Gets fees from db based on code and fee schedule combos.  Includes all provider overrides.  Includes default/no clinic as well as any specified clinic overrides. Although the list always includes extra fees from scheds that we don't need, it's still a very small list.  That list is then used repeatedly by other code in loops to find the actual individual fee amounts.</summary>
		public static List<Fee> GetListFromObjects(List<ProcedureCode> listProcedureCodes,List<string> listMedicalCodes,List<long> listProvNumsTreat,long patPriProv,
			long patSecProv,long patFeeSched,List<InsPlan> listInsPlans,List<long> listClinicNums,List<Appointment> listAppointments,
			List<SubstitutionLink> listSubstitutionLinks,long discountPlanNum
			//listCodeNums,listProvNumsTreat,listProcCodesProvNumDefault,patPriProv,patSecProv,patFeeSched,listInsPlans,listClinicNums
			//List<long> listProcCodesProvNumDefault
			)
		{
			//listMedicalCodes: it already automatically gets the medical codes from procCodes.  This is just for procs. If no procs yet, it will be null.
			//listMedicalCodes can be done by: listProcedures.Select(x=>x.MedicalCode).ToList();  //this is just the strings
			//One way to get listProvNumsTreat is listProcedures.Select(x=>x.ProvNum).ToList()
			//One way to specify a single provNum in listProvNumsTreat is new List<long>(){provNum}
			//One way to get clinicNums is listProcedures.Select(x=>x.ClinicNum).ToList()
			//Another way to get clinicNums is new List<long>(){clinicNum}.
			//These objects will be cleaned up, so they can have duplicates, zeros, invalid keys, nulls, etc
			//In some cases, we need to pass in a list of appointments to make sure we've included all possible providers, both ProvNum and ProvHyg
			//In that case, it's common to leave listProvNumsTreat null because we clearly do not have any of those providers set yet.
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),listProcedureCodes,listMedicalCodes,listProvNumsTreat,patPriProv,
					patSecProv,patFeeSched,listInsPlans,listClinicNums,listAppointments,listSubstitutionLinks,discountPlanNum);
			}
			if(listProcedureCodes==null){
				return new List<Fee>();
			}
			List<long> listCodeNumsOut=new List<long>();
			for(int i=0;i<listProcedureCodes.Count;i++) {
				if(listProcedureCodes[i]==null){
					continue;
				}
				if(!listCodeNumsOut.Contains(listProcedureCodes[i].CodeNum)){
					listCodeNumsOut.Add(listProcedureCodes[i].CodeNum);
				}
				if(ProcedureCodes.IsValidCode(listProcedureCodes[i].MedicalCode)){
					long codeNumMed=ProcedureCodes.GetCodeNum(listProcedureCodes[i].MedicalCode);
					if(!listCodeNumsOut.Contains(codeNumMed)){
						listCodeNumsOut.Add(codeNumMed);
					}
				}
				if(ProcedureCodes.IsValidCode(listProcedureCodes[i].SubstitutionCode)) {
					long codeNumSub=ProcedureCodes.GetCodeNum(listProcedureCodes[i].SubstitutionCode);
					if(!listCodeNumsOut.Contains(codeNumSub)) {
						listCodeNumsOut.Add(codeNumSub);
					}
				}
			}
			if(listMedicalCodes!=null){
				for(int i=0;i<listMedicalCodes.Count;i++) {
					if(ProcedureCodes.IsValidCode(listMedicalCodes[i])){
						long codeNumMed=ProcedureCodes.GetCodeNum(listMedicalCodes[i]);
						if(!listCodeNumsOut.Contains(codeNumMed)){
							listCodeNumsOut.Add(codeNumMed);
						}
					}
				}
			}
			if(listSubstitutionLinks!=null) {
				for(int i=0;i<listSubstitutionLinks.Count;i++) {//Grab all subst codes, since we don't know which ones we will need.
					if(ProcedureCodes.IsValidCode(listSubstitutionLinks[i].SubstitutionCode)){
						long codeNum=ProcedureCodes.GetCodeNum(listSubstitutionLinks[i].SubstitutionCode);
						if(!listCodeNumsOut.Contains(codeNum)){
							listCodeNumsOut.Add(codeNum);
						}
					}
				}
			}
			//Fee schedules. Will potentially include many.=======================================================================================
			List<long> listFeeScheds=new List<long>();
			//Add feesched for first provider (See Claims.CalculateAndUpdate)---------------------------------------------------------------------
			Provider provFirst=Providers.GetFirst();
			if(provFirst!=null && provFirst.FeeSched!=0 && !listFeeScheds.Contains(provFirst.FeeSched)){
				listFeeScheds.Add(provFirst.FeeSched);
			}
			//Add feesched for PracticeDefaultProv------------------------------------------------------------------------------------------------
			Provider provPracticeDefault=Providers.GetProv(PrefC.GetLong(PrefName.PracticeDefaultProv));
			if(provPracticeDefault!=null && provPracticeDefault.FeeSched!=0 && !listFeeScheds.Contains(provPracticeDefault.FeeSched)){
				listFeeScheds.Add(provPracticeDefault.FeeSched);
			}
			//Add feescheds for all treating providers---------------------------------------------------------------------------------------------
			if(listProvNumsTreat!=null){
				for(int i=0;i<listProvNumsTreat.Count;i++) {
					Provider provTreat=Providers.GetProv(listProvNumsTreat[i]);
					if(provTreat!=null && provTreat.FeeSched!=0 && !listFeeScheds.Contains(provTreat.FeeSched)){
						listFeeScheds.Add(provTreat.FeeSched);//treating provs fee scheds
					}
				}
			}
			//Add feescheds for the patient's primary and secondary providers----------------------------------------------------------------------
			Provider providerPatPri=Providers.GetProv(patPriProv);
			if(providerPatPri!=null && providerPatPri.FeeSched!=0 && !listFeeScheds.Contains(providerPatPri.FeeSched)){
				listFeeScheds.Add(providerPatPri.FeeSched);
			}
			Provider providerPatSec=Providers.GetProv(patSecProv);
			if(providerPatSec!=null && providerPatSec.FeeSched!=0 && !listFeeScheds.Contains(providerPatSec.FeeSched)){
				listFeeScheds.Add(providerPatSec.FeeSched);
			}
			//Add feescheds for all procedurecode.ProvNumDefaults---------------------------------------------------------------------------------
			for(int i=0;i<listProcedureCodes.Count;i++) {
				if(listProcedureCodes[i]==null){
					continue;
				}
				long provNumDefault=listProcedureCodes[i].ProvNumDefault;
				if(provNumDefault==0){
					continue;
				}
				Provider provDefault=Providers.GetProv(provNumDefault);
				if(provDefault!=null && provDefault.FeeSched!=0 && !listFeeScheds.Contains(provDefault.FeeSched)){
					listFeeScheds.Add(provDefault.FeeSched);
				}
			}
			//Add feescheds for appointment providers---------------------------------------------------------------------------------------------
			if(listAppointments!=null){
				for(int i=0;i<listAppointments.Count;i++) {
					Provider provAppt=Providers.GetProv(listAppointments[i].ProvNum);
					if(provAppt!=null && provAppt.FeeSched!=0 && !listFeeScheds.Contains(provAppt.FeeSched)){
						listFeeScheds.Add(provAppt.FeeSched);
					}
					Provider provApptHyg=Providers.GetProv(listAppointments[i].ProvHyg);
					if(provApptHyg!=null && provApptHyg.FeeSched!=0 && !listFeeScheds.Contains(provApptHyg.FeeSched)){
						listFeeScheds.Add(provApptHyg.FeeSched);
					}
				}
			}
			//Add feesched for patient.  Rare. --------------------------------------------------------------------------------------------------
			if(patFeeSched!=0){
				if(!listFeeScheds.Contains(patFeeSched)){
					listFeeScheds.Add(patFeeSched);
				}
			}
			//Add feesched for each insplan, both reg and allowed, and Manual Blue Book---------------------------------------------------------------------
			if(listInsPlans!=null){
				for(int i=0;i<listInsPlans.Count;i++) {
					if(listInsPlans[i].FeeSched!=0 && !listFeeScheds.Contains(listInsPlans[i].FeeSched)){
						listFeeScheds.Add(listInsPlans[i].FeeSched);//insplan feeSched
					}
					if(listInsPlans[i].AllowedFeeSched!=0 && !listFeeScheds.Contains(listInsPlans[i].AllowedFeeSched)){
						listFeeScheds.Add(listInsPlans[i].AllowedFeeSched);//allowed feeSched
					}
					if(listInsPlans[i].CopayFeeSched!=0 && !listFeeScheds.Contains(listInsPlans[i].CopayFeeSched)) {
						listFeeScheds.Add(listInsPlans[i].CopayFeeSched);//copay feeSched
					}
					if(listInsPlans[i].ManualFeeSchedNum!=0 && !listFeeScheds.Contains(listInsPlans[i].ManualFeeSchedNum)) {
						listFeeScheds.Add(listInsPlans[i].ManualFeeSchedNum);//manual blue book feeSched
					}
				}
			}
			if(discountPlanNum!=0) {
				long discountPlanFeeSched=DiscountPlans.GetPlan(discountPlanNum).FeeSchedNum;
				if(!listFeeScheds.Contains(discountPlanFeeSched)) {
					listFeeScheds.Add(discountPlanFeeSched);
				}
			}
			//ClinicNums========================================================================================================================
			List<long> listClinicNumsOut=new List<long>();//usually empty or one entry
			if(listClinicNums!=null){
				for(int i=0;i<listClinicNums.Count;i++) {
					if(listClinicNums[i]!=0 && !listClinicNumsOut.Contains(listClinicNums[i])){
						listClinicNumsOut.Add(listClinicNums[i]);//proc ClinicNums
					}
				}
			}
			if(listFeeScheds.Count==0 || listProcedureCodes.Count==0){
				return new List<Fee>();
			}
			string command="SELECT * FROM fee WHERE ClinicNum IN(0";
			if(listClinicNumsOut.Count!=0) {
				command+=","+string.Join(",",listClinicNumsOut.Select(x => POut.Long(x)));
			}
			command+=")";
			if(listFeeScheds.Count!=0) {
				command+= " AND FeeSched IN("+string.Join(",",listFeeScheds.Select(x => POut.Long(x)))+")";
			}
			if(listCodeNumsOut.Count!=0) {
				command+= " AND CodeNum IN("+string.Join(",",listCodeNumsOut.Select(x => POut.Long(x)))+")";
			}
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Gets fees that exactly match criteria: one row per code, the one in effect today. Rows that start later are left out.</summary>
		public static List<Fee> GetListExact(long feeSched,long clinicNum,long provNum){
			Meth.NoCheckMiddleTierRole();
			return GetListExactAsOf(feeSched,clinicNum,provNum,DateTime.Today);
		}

		///<summary>Gets fees that exactly match criteria: one row per code, the one in effect on dateAsOf.</summary>
		public static List<Fee> GetListExactAsOf(long feeSched,long clinicNum,long provNum,DateTime dateAsOf){
			Meth.NoCheckMiddleTierRole();
			dateAsOf=GetDateAsOf(dateAsOf);
			List<Fee> listFees=GetListExactAllDates(feeSched,clinicNum,provNum).FindAll(x => IsInEffect(x,dateAsOf));
			return GetRowsInEffect(listFees,dateAsOf);
		}

		///<summary>Gets every row that exactly matches criteria, whatever its start date: past, current and future rows.</summary>
		public static List<Fee> GetListExactAllDates(long feeSched,long clinicNum,long provNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),feeSched,clinicNum,provNum);
			}
			string command="SELECT * FROM fee WHERE "
				+"FeeSched="+POut.Long(feeSched)+" AND ClinicNum="+POut.Long(clinicNum)+" AND ProvNum="+POut.Long(provNum)+" "
				+"ORDER BY CodeNum,DateEffective,FeeNum";
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Overload gets fees that exactly match criteria with a clinic list: per code and clinic, the row in effect today.</summary>
		public static List<Fee> GetListExact(long feeSched,List<long> listClinicNums,long provNum){
			if(listClinicNums.IsNullOrEmpty()) {
				return new List<Fee>();
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),feeSched,listClinicNums,provNum);
			}
			string command="SELECT * FROM fee WHERE "
				+"FeeSched="+POut.Long(feeSched)+" AND ClinicNum IN ("+string.Join(",",listClinicNums.Select(x => POut.Long(x)))
				+") AND ProvNum="+POut.Long(provNum)+" "
				+"AND DateEffective<="+POut.Date(DateTime.Today);
			return GetRowsInEffect(Crud.FeeCrud.SelectMany(command),DateTime.Today);
		}

		///<summary>Pass in new list and original list.  This will synch everything with Db.  Leave doCheckFeeSchedGroups set to true unless calling from 
		///FeeSchedGroups.</summary>
		public static bool SynchList(List<Fee> listFeesNew,List<Fee> listFeesDb,bool doCheckFeeSchedGroups=true) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetBool(MethodBase.GetCurrentMethod(),listFeesNew,listFeesDb,doCheckFeeSchedGroups);
			}
			//Two rows with the same key and start date are not allowed, but legacy duplicates that this sync leaves untouched don't block it.
			ValidateNoDuplicateDatesForSync(listFeesNew,listFeesDb);
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				FeeSchedGroups.SyncGroupFees(listFeesNew,listFeesDb);
			}
			return Crud.FeeCrud.Sync(listFeesNew,listFeesDb,Security.CurUser.UserNum);
		}

		///<summary>Gets from Db.  Returns the fees associated to the procedure code passed in that are in effect today, one per fee schedule, clinic and
		///provider.</summary>
		public static List<Fee> GetFeesForCode(long codeNum,List<long> listClinicNums=null) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),codeNum,listClinicNums);
			}
			string command="SELECT * FROM fee WHERE CodeNum="+POut.Long(codeNum)+" "
				+"AND DateEffective<="+POut.Date(DateTime.Today)+" ";
			if(listClinicNums!=null && listClinicNums.Count>0) {
				command+="AND ClinicNum IN("+string.Join(",",listClinicNums.Select(x => POut.Long(x)))+")";
			}
			//ordering was being done in the form. Easier to do it here.
			command+=" ORDER BY ClinicNum,ProvNum";
			return GetRowsInEffect(Crud.FeeCrud.SelectMany(command),DateTime.Today);
		}

		///<summary>Gets fees from Db, not including any prov or clinic overrides. One per fee schedule: the row in effect today.</summary>
		public static List<Fee> GetFeesForCodeNoOverrides(long codeNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),codeNum);
			}
			string command="SELECT * FROM fee WHERE CodeNum="+POut.Long(codeNum)+" "
				+"AND ClinicNum=0 AND ProvNum=0 "
				+"AND DateEffective<="+POut.Date(DateTime.Today);
			return GetRowsInEffect(Crud.FeeCrud.SelectMany(command),DateTime.Today);
		}

		///<summary>Returns an amount if a fee has been entered.  Prefers local clinic fees over HQ fees.  Otherwise returns -1.
		///Not usually used directly.  If you don't pass in a list of Fees, it will go directly to the Db. Uses the fee in effect on dateAsOf (see
		///GetDateAsOf).</summary>
		public static double GetAmount(long codeNum,long feeSched,long clinicNum,long provNum,List<Fee> listFees=null,DateTime dateAsOf=default(DateTime)) {
			Meth.NoCheckMiddleTierRole();
			if(FeeScheds.GetIsHidden(feeSched)) {
				return -1;//you cannot obtain fees for hidden fee schedules
			}
			Fee fee=GetFee(codeNum,feeSched,clinicNum,provNum,listFees,dateAsOf);
			if(fee==null) {
				return -1;
			}
			return fee.Amount;
		}

		///<summary>Almost the same as GetAmount.  But never returns -1;  Returns an amount if a fee has been entered.
		///Prefers local clinic fees over HQ fees. Returns 0 if code can't be found.
		///If you don't pass in a list of fees, it will go directly to the database.</summary>
		public static double GetAmount0(long codeNum,long feeSched,long clinicNum=0,long provNum=0,List<Fee> listFees=null,DateTime dateAsOf=default(DateTime)) {
			Meth.NoCheckMiddleTierRole();
			double amountRet=GetAmount(codeNum,feeSched,clinicNum,provNum,listFees,dateAsOf);
			if(amountRet==-1) {
				return 0;
			}
			return amountRet;
		}

		///<summary>Gets the UCR fee for the provided procedure.</summary>
		public static double GetFeeUCR(Procedure procedure) {
			Meth.NoCheckMiddleTierRole();
			long provNum=procedure.ProvNum;
			if(provNum==0) {//if no prov set, then use practice default.
				provNum=PrefC.GetLong(PrefName.PracticeDefaultProv);
			}
			Provider providerFirst=Providers.GetFirst();//Used in order to preserve old behavior...  If this fails, then old code would have failed.
			Provider provider=Providers.GetFirstOrDefault(x => x.ProvNum==provNum)??providerFirst;
			//get the fee based on code and prov fee sched
			double ppoFee=GetAmount0(procedure.CodeNum,provider.FeeSched,procedure.ClinicNum,provNum,dateAsOf:GetDateAsOfForProc(procedure));
			double ucrFee=procedure.ProcFee;
			if(ucrFee > ppoFee) {
				return procedure.Quantity * ucrFee;
			}
			else {
				return procedure.Quantity * ppoFee;
			}
		}

		public static List<Fee> GetManyByFeeNum(List<long> listFeeNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),listFeeNums);
			}
			string command="SELECT * FROM fee WHERE FeeNum IN ("+string.Join(",",listFeeNums)+")";
			return Crud.FeeCrud.SelectMany(command);
		}

		///<summary>Gets one Fee object from the database using the primary key. Returns null if not found.</summary>
		public static Fee GetOneByFeeNum(long feeNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<Fee>(MethodBase.GetCurrentMethod(),feeNum);
			}
			return Crud.FeeCrud.SelectOne(feeNum);
		}

		///<summary>Gets a list of Fees from db. Returns an empty list if not found.</summary>
		public static List<Fee> GetFeesForApi(int limit,int offset,long feeSched,long codeNum,long clinicNum,long provNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				return Meth.GetObject<List<Fee>>(MethodBase.GetCurrentMethod(),limit,offset,feeSched,codeNum,clinicNum,provNum);
			}
			string command="SELECT * from fee"
				+" WHERE SecDateTEdit>="+POut.DateT(DateTime.MinValue);
			if(feeSched>0) {
				command+=" AND FeeSched="+POut.Long(feeSched);
			}
			if(codeNum>0){
				command+=" AND CodeNum="+POut.Long(codeNum);
			}
			if(clinicNum>-1) {
				command+=" AND ClinicNum="+POut.Long(clinicNum);
			}
			if(provNum>-1) {
				command+=" AND ProvNum="+POut.Long(provNum);
			}
			command+=" ORDER BY FeeNum"
				+" LIMIT "+POut.Int(offset)+", "+POut.Int(limit);
			return Crud.FeeCrud.SelectMany(command);
		}
		#endregion Get Methods

		#region Insert
		///<summary>Set doCheckFeeSchedGroups to false when calling this method from FeeSchedGroups to prevent infinitely looping.</summary>
		public static long Insert(Fee fee,bool doCheckFeeSchedGroups=true) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				fee.FeeNum=Meth.GetLong(MethodBase.GetCurrentMethod(),fee,doCheckFeeSchedGroups);
				return fee.FeeNum;
			}
			//Two rows with the same key and start date are not allowed.
			if(IsDuplicateInDb(fee)) {
				throw new ApplicationException(GetDuplicateMessage(fee));
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			fee.SecUserNumEntry=Security.CurUser.UserNum;
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				FeeSchedGroups.UpsertGroupFees(new List<Fee>() { fee });
			}
			return Crud.FeeCrud.Insert(fee);
		}

		///<summary>True if another row with the fee's schedule, code, clinic, provider and start date is in the database.</summary>
		private static bool IsDuplicateInDb(Fee fee) {
			string command="SELECT COUNT(*) FROM fee "
				+"WHERE FeeSched="+POut.Long(fee.FeeSched)+" "
				+"AND CodeNum="+POut.Long(fee.CodeNum)+" "
				+"AND ClinicNum="+POut.Long(fee.ClinicNum)+" "
				+"AND ProvNum="+POut.Long(fee.ProvNum)+" "
				+"AND DateEffective="+POut.Date(fee.DateEffective)+" "
				+"AND FeeNum!="+POut.Long(fee.FeeNum);
			return PIn.Long(Db.GetCount(command))>0;
		}

		/// <summary>Bulk Insert.  Only set doCheckFeeSchedGroups to false from FeeSchedGroups.</summary>
		public static void InsertMany(List<Fee> listFees,bool doCheckFeeSchedGroups=true) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listFees,doCheckFeeSchedGroups);
				return;
			}
			//Two rows with the same key and start date are not allowed, within the batch or against rows already in the database.
			ValidateNoDuplicateDates(listFees);
			List<IGrouping<string,Fee>> listGroupings=listFees.GroupBy(x => x.FeeSched+","+x.ClinicNum+","+x.ProvNum).ToList();
			for(int i=0;i<listGroupings.Count;i++) {
				Fee feeFirst=listGroupings[i].First();
				List<long> listCodeNums=listGroupings[i].Select(x => x.CodeNum).Distinct().ToList();
				string command="SELECT CodeNum,DateEffective FROM fee "
					+"WHERE FeeSched="+POut.Long(feeFirst.FeeSched)+" "
					+"AND ClinicNum="+POut.Long(feeFirst.ClinicNum)+" "
					+"AND ProvNum="+POut.Long(feeFirst.ProvNum)+" "
					+"AND CodeNum IN("+string.Join(",",listCodeNums.Select(x => POut.Long(x)))+")";
				HashSet<string> hashSetExisting=new HashSet<string>(Db.GetTable(command).Select()
					.Select(x => PIn.Long(x["CodeNum"].ToString())+","+PIn.Date(x["DateEffective"].ToString()).ToString("yyyy-MM-dd")));
				Fee feeDuplicate=listGroupings[i].FirstOrDefault(x => hashSetExisting.Contains(x.CodeNum+","+x.DateEffective.ToString("yyyy-MM-dd")));
				if(feeDuplicate!=null) {
					throw new ApplicationException(GetDuplicateMessage(feeDuplicate));
				}
			}
			//Security.CurUser.UserNum gets set on MT by the DtoProcessor so it matches the user from the client WS.
			for(int i=0;i<listFees.Count;i++) {
				listFees[i].SecUserNumEntry=Security.CurUser.UserNum;
			}
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				FeeSchedGroups.UpsertGroupFees(listFees);
			}
			Crud.FeeCrud.InsertMany(listFees);
		}
		#endregion Insert

		#region Update
		///<summary>Only set doCheckFeeSchedGroups to false from FeeSchedGroups.</summary>
		public static void Update(Fee fee,Fee feeOld=null,bool doCheckFeeSchedGroups=true) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),fee,feeOld,doCheckFeeSchedGroups);
				return;
			}
			//Check if this fee is associated to a FeeSchedGroup and update the rest of the group as needed.
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				FeeSchedGroups.UpsertGroupFees(new List<Fee>() { fee });
			}
			if(feeOld!=null) {
				Crud.FeeCrud.Update(fee,feeOld);
			}
			else {
				Crud.FeeCrud.Update(fee);
			}
		}
		#endregion Update

		#region Delete
		///<summary>Only set doCheckFeeSchedGroups to false from FeeSchedGroups.</summary>
		public static void Delete(Fee fee,bool doCheckFeeSchedGroups=true) {
			//Even though we do not run a query in this method, there is a lot of back and forth and we should get to the server early to ensure less chattiness.
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),fee,doCheckFeeSchedGroups);
				return;
			}
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				//If this fee isn't in a group don't bother checking.
				if(FeeSchedGroups.GetOneForFeeSchedAndClinic(fee.FeeSched,fee.ClinicNum)!=null) {
					FeeSchedGroups.DeleteGroupFees(new List<long>() { fee.FeeNum });
				}
			}
			Delete(fee.FeeNum);
		}

		///<summary></summary>
		public static void Delete(long feeNum){
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeNum);
				return;
			}
			ClearFkey(feeNum);
			string command="DELETE FROM fee WHERE FeeNum="+feeNum;
			Db.NonQ(command);
		}

		///<summary>Only set doCheckFeeSchedGroups to false from FeeSchedGroups.</summary>
		public static void DeleteMany(List<long> listFeeNums,bool doCheckFeeSchedGroups=true) {
			if(listFeeNums.Count==0) {
				return;
			}
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listFeeNums,doCheckFeeSchedGroups);
				return;
			}
			if(PrefC.GetBool(PrefName.ShowFeeSchedGroups) && doCheckFeeSchedGroups) {
				FeeSchedGroups.DeleteGroupFees(listFeeNums);
			}
			ClearFkey(listFeeNums);
			string command="DELETE FROM fee WHERE FeeNum IN ("+string.Join(",",listFeeNums)+")";
			Db.NonQ(command);
		}

		///<summary>Deletes all fees for the supplied FeeSched that aren't for the HQ clinic.</summary>
		public static void DeleteNonHQFeesForSched(long feeSched) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeSched);
				return;
			}
			string command="SELECT FeeNum FROM fee WHERE FeeSched="+POut.Long(feeSched)+" AND ClinicNum!=0";
			List<long> listFeeNums=Db.GetListLong(command);
			DeleteMany(listFeeNums);
		}

		///<summary>Deletes the fees with the exact specified FeeSchedule, ClinicNum, and ProvNum combination that start on or after dateFrom. A dateFrom
		///before 1880 deletes every row of the combination, like DeleteFees. Used by Copy To with an effective date: rows that start before the copied
		///fees (the current fees and their history) are kept.</summary>
		public static void DeleteFeesOnOrAfter(long feeSched,long clinicNum,long provNum,DateTime dateFrom) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeSched,clinicNum,provNum,dateFrom);
				return;
			}
			string command="DELETE FROM fee WHERE "
				+"FeeSched="+POut.Long(feeSched)+" AND ClinicNum="+POut.Long(clinicNum)+" AND ProvNum="+POut.Long(provNum);
			if(dateFrom.Year>1880) {
				command+=" AND DateEffective>="+POut.Date(dateFrom);
			}
			Db.NonQ(command);
		}

		/// <summary>Deletes all fees with the exact specified FeeSchedule, ClinicNum, and ProvNum combination, whatever their start date.</summary>
		public static void DeleteFees(long feeSched,long clinicNum,long provNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeSched,clinicNum,provNum);
				return;
			}
			string command="DELETE FROM fee WHERE "
				+"FeeSched="+POut.Long(feeSched)+" AND ClinicNum="+POut.Long(clinicNum)+" AND ProvNum="+POut.Long(provNum);
			Db.NonQ(command);
		}
		#endregion Delete

		#region Fee tools
		///<summary>One line of a fee file read by ImportFees.</summary>
		public class FeeImportLine {
			///<summary>First column.</summary>
			public string ProcCode="";
			///<summary>Second column, trimmed. Blank means "delete the fee".</summary>
			public string AmountText="";
			///<summary>True when the line has a date in the fifth column (the format written by export with future fees).</summary>
			public bool HasDate;
			///<summary>The fifth column when HasDate.</summary>
			public DateTime DateEffective;
		}

		///<summary>Reads fee file lines: tab separated, procedure code, amount, then (ignored) abbreviation and description, then an optional start date
		///as yyyy-MM-dd. The date column is only written by an export that includes future fees; a blank date column, or a file without one, uses the
		///date chosen in the import window. Lines with fewer than two columns are skipped. No database access.</summary>
		public static List<FeeImportLine> ParseImportLines(IEnumerable<string> listLines) {
			Meth.NoCheckMiddleTierRole();
			List<FeeImportLine> listFeeImportLines=new List<FeeImportLine>();
			foreach(string line in listLines) {
				if(line==null) {
					continue;
				}
				string[] arrayFields=line.Split(new string[1] {"\t"},StringSplitOptions.None);
				if(arrayFields.Length<2) {
					continue;
				}
				FeeImportLine feeImportLine=new FeeImportLine();
				feeImportLine.ProcCode=arrayFields[0];
				feeImportLine.AmountText=arrayFields[1].Trim();
				if(arrayFields.Length>=5 && arrayFields[4].Trim()!="") {
					DateTime dateEffective;
					if(DateTime.TryParseExact(arrayFields[4].Trim(),"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture,
						System.Globalization.DateTimeStyles.None,out dateEffective))
					{
						feeImportLine.HasDate=true;
						feeImportLine.DateEffective=dateEffective.Date;
					}
				}
				listFeeImportLines.Add(feeImportLine);
			}
			return listFeeImportLines;
		}

		///<summary>Imports fee file lines into one fee schedule, clinic and provider. Each fee gets the line's date, or dateEffectiveDefault when the
		///line has none (a date before 1880 means "always in effect"). An existing row with exactly that key and date is updated, otherwise a row is
		///inserted, so importing next year's fees with a future date leaves the current fees alone. A blank amount deletes the row with that date, if
		///there is one. When the same code and date appear twice in the file, the first line wins. Makes the same security log entries as the old
		///import. Fires progress events. Returns the number of fees inserted, updated or deleted.</summary>
		public static int ImportFees(List<string> listLines,long feeSchedNum,long clinicNum,long provNum,DateTime dateEffectiveDefault) {
			Meth.NoCheckMiddleTierRole();
			List<FeeImportLine> listFeeImportLines=ParseImportLines(listLines);
			List<Fee> listFeesDb=GetListExactAllDates(feeSchedNum,clinicNum,provNum);
			HashSet<string> hashSetDone=new HashSet<string>();
			string feeSchedDesc=FeeScheds.GetDescription(feeSchedNum);
			int countChanged=0;
			for(int i=0;i<listFeeImportLines.Count;i++) {
				FeeImportLine feeImportLine=listFeeImportLines[i];
				FireImportProgress(i,listFeeImportLines.Count);
				long codeNum=ProcedureCodes.GetCodeNum(feeImportLine.ProcCode);
				if(codeNum==0) {
					continue;
				}
				DateTime dateEffective=(feeImportLine.HasDate ? feeImportLine.DateEffective : dateEffectiveDefault);
				dateEffective=(dateEffective.Year<1880 ? DateEffectiveAlways : dateEffective.Date);
				if(!hashSetDone.Add(codeNum+","+dateEffective.ToString("yyyy-MM-dd"))) {
					continue;//this code and date was already in the file
				}
				Fee fee=GetFeeExactDate(listFeesDb,codeNum,feeSchedNum,clinicNum,provNum,dateEffective);
				string feeOldStr="";
				DateTime datePrevious=DateTime.MinValue;
				if(fee!=null) {
					feeOldStr="Old Fee: "+fee.Amount.ToString("c")+", ";
					datePrevious=fee.SecDateTEdit;
				}
				if(feeImportLine.AmountText=="") {//an empty entry deletes an existing fee, but does not insert a blank override
					if(fee==null) {
						continue;
					}
					Delete(fee);
					listFeesDb.Remove(fee);
					SecurityLogs.MakeLogEntry(EnumPermType.ProcFeeEdit,0,"Procedure: "+feeImportLine.ProcCode+", "
						+feeOldStr
						+"Fee Schedule: "+feeSchedDesc+GetDateEffectiveLogText(dateEffective)+". "
						+"Fee deleted using the Import button in the Fee Tools window.",codeNum,DateTime.MinValue);
					SecurityLogs.MakeLogEntry(EnumPermType.LogFeeEdit,0,"Fee deleted",fee.FeeNum,datePrevious);
					countChanged++;
					continue;
				}
				double amount=PIn.Double(feeImportLine.AmountText);
				if(fee==null) {
					fee=new Fee();
					fee.Amount=amount;
					fee.FeeSched=feeSchedNum;
					fee.CodeNum=codeNum;
					fee.ClinicNum=clinicNum;//Either 0 because you're importing on an HQ schedule or local clinic because the feesched is localizable.
					fee.ProvNum=provNum;
					fee.DateEffective=dateEffective;
					Insert(fee);
					listFeesDb.Add(fee);
				}
				else {
					fee.Amount=amount;
					Update(fee);
				}
				SecurityLogs.MakeLogEntry(EnumPermType.ProcFeeEdit,0,"Procedure: "+feeImportLine.ProcCode+", "
					+feeOldStr
					+"New Fee: "+fee.Amount.ToString("c")+", "
					+"Fee Schedule: "+feeSchedDesc+GetDateEffectiveLogText(dateEffective)+". "
					+"Fee changed using the Import button in the Fee Tools window.",codeNum,DateTime.MinValue);
				SecurityLogs.MakeLogEntry(EnumPermType.LogFeeEdit,0,"Fee changed",fee.FeeNum,datePrevious);
				countChanged++;
			}
			return countChanged;
		}

		private static void FireImportProgress(int index,int count) {
			if(count==0) {
				return;
			}
			double percent=(double)index*100d/(double)count;
			ODEvent.Fire(ODEventType.ProgressBar,new ProgressBarHelper(
				"Importing fees...",((int)percent).ToString(),blockValue:(int)percent,progressStyle:ProgBarStyle.Blocks));
		}

		///<summary>Saves the result of Increase by %. listFeesBase are the rows the increase starts from (one per code, normally the rows in effect on
		///the base date for one fee schedule, clinic and provider). Amounts of 0 and -1 (blank override) are left alone. With no new effective date
		///(before 1880) the base rows are updated in place, as before effective dates existed. With a new effective date, each result goes to the row
		///with that key and date: updated if it exists, inserted otherwise; the base rows are not changed. Fee schedule groups are kept in sync.
		///Returns the saved rows (with the new amounts), for logging.</summary>
		public static List<Fee> SaveIncrease(List<Fee> listFeesBase,int percent,int round,DateTime dateEffectiveNew) {
			Meth.NoCheckMiddleTierRole();
			List<Fee> listFeesIncreased=IncreaseNew(0,percent,round,listFeesBase,0,0).FindAll(x => x.Amount!=0 && x.Amount!=-1);
			List<Fee> listFeesSaved=new List<Fee>();
			if(dateEffectiveNew.Year<1880) {
				if(PrefC.GetBool(PrefName.ShowFeeSchedGroups)) {
					FeeSchedGroups.UpsertGroupFees(listFeesIncreased);
				}
				for(int i=0;i<listFeesIncreased.Count;i++) {
					Update(listFeesIncreased[i],doCheckFeeSchedGroups:false);
					listFeesSaved.Add(listFeesIncreased[i]);
				}
				return listFeesSaved;
			}
			dateEffectiveNew=dateEffectiveNew.Date;
			Dictionary<string,List<Fee>> dictFeesByCombo=new Dictionary<string,List<Fee>>();
			for(int i=0;i<listFeesIncreased.Count;i++) {
				Fee feeIncreased=listFeesIncreased[i];
				string combo=feeIncreased.FeeSched+","+feeIncreased.ClinicNum+","+feeIncreased.ProvNum;
				if(!dictFeesByCombo.TryGetValue(combo,out List<Fee> listFeesCombo)) {
					listFeesCombo=GetListExactAllDates(feeIncreased.FeeSched,feeIncreased.ClinicNum,feeIncreased.ProvNum);
					dictFeesByCombo[combo]=listFeesCombo;
				}
				Fee feeAtDate=GetFeeExactDate(listFeesCombo,feeIncreased.CodeNum,feeIncreased.FeeSched,feeIncreased.ClinicNum,feeIncreased.ProvNum,
					dateEffectiveNew);
				if(feeAtDate==null) {
					Fee feeNew=new Fee();
					feeNew.FeeSched=feeIncreased.FeeSched;
					feeNew.CodeNum=feeIncreased.CodeNum;
					feeNew.ClinicNum=feeIncreased.ClinicNum;
					feeNew.ProvNum=feeIncreased.ProvNum;
					feeNew.Amount=feeIncreased.Amount;
					feeNew.DateEffective=dateEffectiveNew;
					Insert(feeNew);//also writes the other clinics of a fee schedule group
					listFeesCombo.Add(feeNew);
					listFeesSaved.Add(feeNew);
					continue;
				}
				feeAtDate.Amount=feeIncreased.Amount;
				Update(feeAtDate);//also writes the other clinics of a fee schedule group
				listFeesSaved.Add(feeAtDate);
			}
			return listFeesSaved;
		}

		///<summary>For an export that includes future fees: the fees that take over after dateAsOf for one code, as the lookup for this schedule, clinic
		///and provider would find them. Returns one row per date on which the looked-up fee changes, in date order. listFees must hold every row that
		///could apply (all dates). No database access except FeeScheds.IsGlobal.</summary>
		public static List<Fee> GetFutureChanges(List<Fee> listFees,long codeNum,long feeSchedNum,long clinicNum,long provNum,DateTime dateAsOf) {
			Meth.NoCheckMiddleTierRole();
			return GetFutureChanges(listFees,codeNum,feeSchedNum,clinicNum,provNum,FeeScheds.IsGlobal(feeSchedNum),dateAsOf);
		}

		///<summary>Same as the other overload, with isGlobal given so it needs no database.</summary>
		public static List<Fee> GetFutureChanges(List<Fee> listFees,long codeNum,long feeSchedNum,long clinicNum,long provNum,bool isGlobal,DateTime dateAsOf) {
			Meth.NoCheckMiddleTierRole();
			dateAsOf=dateAsOf.Date;
			List<Fee> listFeesRet=new List<Fee>();
			List<DateTime> listDates=listFees.FindAll(x => x.CodeNum==codeNum && x.FeeSched==feeSchedNum && x.DateEffective.Date>dateAsOf)
				.Select(x => x.DateEffective.Date).Distinct().OrderBy(x => x).ToList();
			Fee feePrevious=GetFeeFromList(listFees,codeNum,feeSchedNum,clinicNum,provNum,isGlobal,dateAsOf);
			for(int i=0;i<listDates.Count;i++) {
				Fee fee=GetFeeFromList(listFees,codeNum,feeSchedNum,clinicNum,provNum,isGlobal,listDates[i]);
				if(fee==null || fee==feePrevious) {
					continue;
				}
				listFeesRet.Add(fee);
				feePrevious=fee;
			}
			return listFeesRet;
		}
		#endregion Fee tools

		#region Misc Methods
		///<summary>Increases the fees passed in by percent.Round should be the number of decimal places, either 0,1,or 2.
		///This method will not manipulate listFees passed in, although there is no particular reason for this choice.
		///Simply increases every fee passed in by the percent specified and returns the results.
		///The following parameters are ignored: feeSchedNum, clinicNum, and provNum.</summary>
		public static List<Fee> IncreaseNew(long feeSchedNum,int percent,int round,List<Fee> listFees,long clinicNum,long provNum) {
			Meth.NoCheckMiddleTierRole();
			List<Fee> listFeesRetVal=new List<Fee>();
			for(int i=0;i<listFees.Count;i++) {
				if(listFees[i].Amount==0 || listFees[i].Amount==-1){
					listFeesRetVal.Add(listFees[i].Copy());
					continue;
				}
				double newVal=(double)listFees[i].Amount*(1+(double)percent/100);
				if(round>0) {
					newVal=Math.Round(newVal,round);
				}
				else {
					newVal=Math.Round(newVal,MidpointRounding.AwayFromZero);
				}
				Fee feeNew=listFees[i].Copy();
				feeNew.Amount=newVal;
				listFeesRetVal.Add(feeNew);
			}
			return listFeesRetVal;
		}

		///<summary>Zeros securitylog FKey column for rows that are using the matching feeNum as FKey and are related to Fee.
		///Permtypes are generated from the AuditPerms property of the CrudTableAttribute within the Fee table type.</summary>
		public static void ClearFkey(long feeNum) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),feeNum);
				return;
			}
			Crud.FeeCrud.ClearFkey(feeNum);
		}

		///<summary>Zeros securitylog FKey column for rows that are using the matching feeNums as FKey and are related to Fee.
		///Permtypes are generated from the AuditPerms property of the CrudTableAttribute within the Fee table type.</summary>
		public static void ClearFkey(List<long> listFeeNums) {
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				Meth.GetVoid(MethodBase.GetCurrentMethod(),listFeeNums);
				return;
			}
			Crud.FeeCrud.ClearFkey(listFeeNums);
		}

		///<summary>Returns true if the feeAmtNewStr is an amount that does not match fee, either because fee is null and feeAmtNewStr is not, or because
		///fee not null and the feeAmtNewStr is an equal amount, including a blank entry.</summary>
		public static bool IsFeeAmtEqual(Fee fee,string feeAmtNewStr) {
			//There is no fee in the database and the user didn't set a new fee value so there is no change.
			if(fee==null && feeAmtNewStr=="") {
				return true;
			}
			//Fee exists, but new amount is the same.
			if(fee!=null && (feeAmtNewStr!="" && fee.Amount==PIn.Double(feeAmtNewStr) || (fee.Amount==-1 && feeAmtNewStr==""))) {
				return true;
			}
			return false;
		}
		#endregion Misc Methods
	}

}