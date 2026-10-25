using CodeBase;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OpenDentBusiness.Misc {
	public class SecurityHash {
		///<summary>The date Open Dental started hashing fields into various SecurityHash columns. Used to determine if hashing is required. </summary>
		public static DateTime DateStart=new DateTime(2025,3,27);
		///<summary>Only set to false for standalone hashing tool. </summary>
		public static bool IsThreaded=true;
		private static bool _arePaySplitsUpdated=false;
		private static bool _areAppointmentsUpdated=false;
		private static bool _arePatientsUpdated=false;
		private static bool _arePayPlansUpdated=false;
		private static bool _areClaimsUpdated=false;
		private static bool _areClaimProcsUpdated=false;
		private static bool _areInsVerifiesUpdated=false;
		private static bool _areInsVerifyHistsUpdated=false;
		private static bool _areInsSubsUpdated=false;
		private static List<Thread> _listHashThreads=new List<Thread>();
		///<summary>Rows per statement when the whole patient table is rehashed (RehashPatientTable).</summary>
		public const int PATIENT_BATCH_SIZE=1000;

		///<summary>This method is NOT safe to invoke during database conversions. It is only to be called AFTER all conversions have taken place in order to avoid a rare table lockup. 
		///Runs a hashing algorithm over all tables for which Open Dental is enforcing database integrity. Overwrites any existing SecurityHashes with new ones for recent entries, except Patient table.
		///Patient rows are hashed only when their SecurityHash is blank or was written by Open Dental (see IsLegacyHash); RehashPatientTable rehashes
		///them all. Tables: appointment, claim, claimproc, patient, payplan, paysplit, and the 25.1/25.2 columns on inssub, insverify and insverifyhist
		///(rows edited on or after GetHashingDate).
		///This is also the admin rehash step after moving a database over from Open Dental. It runs automatically on the workstation that performs
		///the next program version update (PrefL). RehashNow runs it on demand. Either way, it converts Open Dental's hashes for every patient, and for
		///rows in the verified date range of the other tables, to the current TamperHash format. See CodeBase/Crypto/README.md.</summary>
		public static void UpdateHashing() {
			//Ordered by threaded then non-threaded, then largest to smallest
			RunClaimProc();
			RunPaysplit();
			RunPatient();
			RunAppointment();
			RunClaim();
			RunPayPlan();
			RunInsSub();
			RunInsVerify();
			RunInsVerifyHist();
			ThreadJoinHelper();//Used to dispose of HashAlgorithms in the threads above
			string updating="Updating database.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
		}

		///<summary>Only used one time during a conversion script. Sets the _arePatientsUpdate boolean to false. Allows the hashing logic to run on the patient table a subsequent time during a convert script update. </summary>
		public static void ResetPatientHashing() {
			_arePatientsUpdated=false;
		}

		///<summary>Admin rehash: runs UpdateHashing on the calling thread, even if it already ran in this process, and returns when every table is done.
		///Use it once after moving a database over from Open Dental so Open Dental's SecurityHash values are replaced with current ones.
		///Database Maintenance's Tools tab runs it as "Security Hashes".
		///Not safe during database conversions, same as UpdateHashing.</summary>
		public static void RehashNow() {
			bool isThreadedOld=IsThreaded;
			IsThreaded=false;
			try {
				_arePaySplitsUpdated=false;
				_areAppointmentsUpdated=false;
				_arePatientsUpdated=false;
				_arePayPlansUpdated=false;
				_areClaimsUpdated=false;
				_areClaimProcsUpdated=false;
				_areInsVerifiesUpdated=false;
				_areInsVerifyHistsUpdated=false;
				_areInsSubsUpdated=false;
				UpdateHashing();
			}
			finally {
				IsThreaded=isThreadedOld;
			}
		}

		///<summary>Returns either SecurityHash.DateStart or the date from a month prior to today, whichever is more recent.</summary>
		public static DateTime GetHashingDate() {
			DateTime dateLastMonth=DateTime.Now.Date.AddMonths(-1);
			DateTime dateStartHashing=DateStart;
			if(dateLastMonth>DateStart) {
				dateStartHashing=dateLastMonth;
			}
			return dateStartHashing;
		}

		///<summary>Compares a row's stored SecurityHash with the hash computed from its current values. Returns true if they match, or if the stored
		///hash is a legacy hash written by Open Dental (see IsLegacyHash). Open Dental's hash key is proprietary, so those can't be checked and
		///are not reported as tampering. They are replaced the next time the row is saved, or by UpdateHashing.</summary>
		public static bool IsHashValid(string hashStored,string hashComputed) {
			if(hashStored==hashComputed) {
				return true;
			}
			return IsLegacyHash(hashStored);
		}

		///<summary>True if hash is non-empty and not in the current TamperHash.Compute format, which means Open Dental wrote it before this
		///database was moved to this program. A blank hash is not legacy: it still counts as unhashed.</summary>
		public static bool IsLegacyHash(string hash) {
			return !string.IsNullOrEmpty(hash) && !TamperHash.IsCurrentFormat(hash);
		}

		private static void RunPaysplit() {
			if(_arePaySplitsUpdated) {
				return;
			}
			//Check if columns are present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"paysplit","SecurityHash")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"paysplit","PatNum")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"paysplit","SplitAmt")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"paysplit","DatePay"))
			{
				return;
			}
			string updating="Hashing paysplits.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			//Threading because it can take too long and it doesn't matter if user starts working while it's still in progress.
			//They might see integrity triangles until this completes.
			//If they shut down before it completes, then those won't be fixed until the next time they run the hashing (their next update).
			//This is only a problem for a recent db conversion, where there are thousands of splits with the same date.  Otherwise, it's really fast.
			_arePaySplitsUpdated=true;
			if(IsThreaded){
				ThreadStart threadStart=new ThreadStart(PaysplitWorker);
				Thread thread=new Thread(threadStart);
				thread.IsBackground=true;
				thread.Start();
				_listHashThreads.Add(thread);
			}
			else{
				PaysplitWorker();
			}
		}

		private static void PaysplitWorker() {
			DateTime dateStartHashing=GetHashingDate();
			//Hash entries made after new date
			string command="SELECT SplitNum, PatNum, SplitAmt, DatePay, SecurityHash FROM paysplit WHERE DatePay >= "+POut.Date(dateStartHashing);
			DataTable table=Db.GetTable(command);
			long splitNum;
			string unhashedText="";
			string hashedTextOld="";
			string hashedTextNew="";
			for(int i=0;i<table.Rows.Count;i++) {
				unhashedText="";
				hashedTextOld=PIn.String(table.Rows[i]["SecurityHash"].ToString());
				splitNum=PIn.Long(table.Rows[i]["SplitNum"].ToString());
				unhashedText+=PIn.Long(table.Rows[i]["PatNum"].ToString());
				unhashedText+=PIn.Double(table.Rows[i]["SplitAmt"].ToString()).ToString("F2");
				unhashedText+=PIn.Date(table.Rows[i]["DatePay"].ToString()).ToString("yyyy-MM-dd");
				try {
					hashedTextNew=TamperHash.Compute(unhashedText);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedTextNew=ex.GetType().Name;
				}
				//Only update hashes that changed
				if(hashedTextOld!=hashedTextNew) {
					command=$@"UPDATE paysplit SET SecurityHash='{POut.String(hashedTextNew)}'{KeepTimestamps("paysplit")} WHERE SplitNum={POut.Long(splitNum)}";
					Db.NonQ(command);
				}
			}
		}

		private static void RunAppointment() {
			if(_areAppointmentsUpdated) {
				return;
			}
			//Check if columns are present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"appointment","SecurityHash")
			  || !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"appointment","AptStatus")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"appointment","Confirmed")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"appointment","AptDateTime"))
			{
				return;
			}
			string updating="Hashing appointments.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			DateTime dateStartHashing=GetHashingDate();
			//Hash entries made after new date
			string command="SELECT AptNum, AptStatus, Confirmed, AptDateTime, SecurityHash FROM appointment WHERE AptDateTime>= "+POut.Date(dateStartHashing);
			DataTable table=Db.GetTable(command);
			long aptNum;
			string unhashedText="";
			string hashedTextOld="";
			string hashedTextNew="";
			for(int i=0;i<table.Rows.Count;i++) {
				unhashedText="";
				hashedTextOld=PIn.String(table.Rows[i]["SecurityHash"].ToString());
				aptNum=PIn.Long(table.Rows[i]["AptNum"].ToString());
				unhashedText+=PIn.Int(table.Rows[i]["AptStatus"].ToString()).ToString();
				unhashedText+=PIn.Long(table.Rows[i]["Confirmed"].ToString()).ToString();
				unhashedText+=PIn.DateT(table.Rows[i]["AptDateTime"].ToString()).ToString("yyyy-MM-dd HH:mm:ss");
				try {
					hashedTextNew=TamperHash.Compute(unhashedText);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedTextNew=ex.GetType().Name;
				}
				//Only update hashes that changed
				if(hashedTextOld!=hashedTextNew) {
					command=$@"UPDATE appointment SET SecurityHash='{POut.String(hashedTextNew)}'{KeepTimestamps("appointment")} WHERE AptNum={POut.Long(aptNum)}";
					Db.NonQ(command);
				}
			}
			_areAppointmentsUpdated=true;
		}

		private static void RunPatient() {
			if(_arePatientsUpdated) {
				return;
			}
			//Check if SecurityHash column is present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"patient","SecurityHash")) {
				return;
			}
			string updating="Hashing patients.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			_arePatientsUpdated=true;
			//Threading because it can take too long and it doesn't matter if user starts working while it's still in progress.
			//They might see integrity triangles until this completes.
			//If they shut down before it completes, then those won't be fixed until the next time they run the hashing (their next update).
			if(IsThreaded){
				ThreadStart threadStart=new ThreadStart(PatientWorker);
				Thread thread=new Thread(threadStart);
				thread.IsBackground=true;
				thread.Start();
				_listHashThreads.Add(thread);
			}
			else{
				PatientWorker();
			}
		}

		private static void PatientWorker() {
			//DateTime dateStartHashing=GetHashingDate();
			//Blank hashes, plus legacy hashes written by Open Dental (anything without the current TamperHash prefix), which can't be verified.
			string command="SELECT PatNum FROM patient WHERE SecurityHash NOT LIKE '"+POut.String(TamperHash.Prefix)+"%'";
			DataTable table=Db.GetTable(command);
			long patNum;
			string unhashedText="";
			string hashedText="";
			for(int i=0;i<table.Rows.Count;i++) {
				patNum=PIn.Long(table.Rows[i]["PatNum"].ToString());
				unhashedText=patNum.ToString();
				try {
					hashedText=TamperHash.Compute(unhashedText,isPatientTable:true);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedText=ex.GetType().Name;
				}
				command=$@"UPDATE patient SET SecurityHash='{POut.String(hashedText)}'{KeepTimestamps("patient")} WHERE PatNum={POut.Long(patNum)}";
				Db.NonQ(command);
			}
		}

		private static void RunPayPlan() {
			if(_arePayPlansUpdated) {
				return;
			}
			//Check if columns are present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"payplan","SecurityHash")
			  || !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"payplan","Guarantor")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"payplan","PayAmt")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"payplan","IsClosed")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"payplan","IsLocked"))
			{
				return;
			}
			string updating="Hashing payplans.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			//Clear old hashes
			string command="UPDATE payplan SET SecurityHash=''"+KeepTimestamps("payplan")+" WHERE SecurityHash!=''";
			Db.NonQ(command);
			DateTime dateStartHashing=GetHashingDate();
			//Hash entries made after new date
			command="SELECT * FROM payplan WHERE PayPlanDate>= "+POut.Date(dateStartHashing);
			DataTable table=Db.GetTable(command);
			long payPlanNum;
			string unhashedText="";
			string hashedText="";
			for(int i=0;i<table.Rows.Count;i++) {
				unhashedText="";
				payPlanNum=PIn.Long(table.Rows[i]["PayPlanNum"].ToString());
				unhashedText+=PIn.Long(table.Rows[i]["Guarantor"].ToString());
				unhashedText+=PIn.Double(table.Rows[i]["PayAmt"].ToString()).ToString("F2");
				unhashedText+=PIn.Bool(table.Rows[i]["IsClosed"].ToString());
				unhashedText+=PIn.Bool(table.Rows[i]["IsLocked"].ToString());
				try {
					hashedText=TamperHash.Compute(unhashedText);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedText=ex.GetType().Name;
				}
				command=$@"UPDATE payplan SET SecurityHash='{POut.String(hashedText)}'{KeepTimestamps("payplan")} WHERE PayPlanNum={POut.Long(payPlanNum)}";
				Db.NonQ(command);
			}
			_arePayPlansUpdated=true;
		}

		private static void RunClaim() {
			if(_areClaimsUpdated) {
				return;
			}
			//Check if columns are present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claim","SecurityHash")
			  || !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claim","ClaimFee")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claim","ClaimStatus")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claim","InsPayEst")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claim","InsPayAmt"))
			{
				return;
			}
			string updating="Hashing claims.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			DateTime dateStartHashing=GetHashingDate();
			//Hash entries made after new date
			string command="SELECT ClaimNum, ClaimFee, ClaimStatus, InsPayEst, InsPayAmt, SecurityHash FROM claim WHERE DateService >= "+POut.Date(dateStartHashing);
			DataTable table=Db.GetTable(command);
			long claimNum;
			string unhashedText="";
			string hashedText="";
			string hashedTextOld="";
			for(int i=0;i<table.Rows.Count;i++) {
				unhashedText="";
				claimNum=PIn.Long(table.Rows[i]["ClaimNum"].ToString());
				hashedTextOld=PIn.String(table.Rows[i]["SecurityHash"].ToString());
				unhashedText+=PIn.Double(table.Rows[i]["ClaimFee"].ToString()).ToString("F2");
				unhashedText+=PIn.String(table.Rows[i]["ClaimStatus"].ToString());
				unhashedText+=PIn.Double(table.Rows[i]["InsPayEst"].ToString()).ToString("F2");
				unhashedText+=PIn.Double(table.Rows[i]["InsPayAmt"].ToString()).ToString("F2");
				try {
					hashedText=TamperHash.Compute(unhashedText);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedText=ex.GetType().Name;
				}
				if(hashedTextOld!=hashedText) { //Only update SecurityHash if it's different.
					command=$@"UPDATE claim SET SecurityHash='{POut.String(hashedText)}'{KeepTimestamps("claim")} WHERE ClaimNum={POut.Long(claimNum)}";
					Db.NonQ(command);
				}
			}
			_areClaimsUpdated=true;
		}

		private static void RunClaimProc() {
			if(_areClaimProcsUpdated) {
				return;
			}
			//Check if columns are present
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claimproc","SecurityHash")
			  || !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claimproc","ClaimNum")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claimproc","Status")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claimproc","InsPayEst")
				|| !LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"claimproc","InsPayAmt"))
			{
				return;
			}
			string updating="Hashing claimprocs.";
			ODEvent.Fire(ODEventType.ConvertDatabases,updating);
			//Threading because it can take too long and it doesn't matter if user starts working while it's still in progress.
			//They might see integrity triangles until this completes.
			//If they shut down before it completes, then those won't be fixed until the next time they run the hashing (their next update).
			_areClaimProcsUpdated=true;
			if(IsThreaded){
				ThreadStart threadStart=new ThreadStart(ClaimProcWorker);
				Thread thread=new Thread(threadStart);
				thread.IsBackground=true;
				thread.Start();
				_listHashThreads.Add(thread);
			}
			else{
				ClaimProcWorker();
			}
		}

		private static void ClaimProcWorker() {
			DateTime dateStartHashing=GetHashingDate();
			//Hash entries made after new date
			string command="SELECT ClaimProcNum, ClaimNum, Status, InsPayEst, InsPayAmt, SecurityHash FROM claimproc WHERE SecDateEntry >= "+POut.Date(dateStartHashing);
			DataTable table=Db.GetTable(command);
			long claimProcNum;
			string unhashedText="";
			string hashedText="";
			string hashedTextOld="";
			for(int i=0;i<table.Rows.Count;i++) {
				unhashedText="";
				claimProcNum=PIn.Long(table.Rows[i]["ClaimProcNum"].ToString());
				hashedTextOld=PIn.String(table.Rows[i]["SecurityHash"].ToString());
				unhashedText+=PIn.Long(table.Rows[i]["ClaimNum"].ToString());
				unhashedText+=PIn.Int(table.Rows[i]["Status"].ToString());
				unhashedText+=PIn.Double(table.Rows[i]["InsPayEst"].ToString()).ToString("F2");
				unhashedText+=PIn.Double(table.Rows[i]["InsPayAmt"].ToString()).ToString("F2");
				try {
					hashedText=TamperHash.Compute(unhashedText);
				}
				catch(Exception ex) {
					ex.DoNothing();
					hashedText=ex.GetType().Name;
				}
				if(hashedTextOld!=hashedText) { //Only update SecurityHash if it's different.
					command=$@"UPDATE claimproc SET SecurityHash='{POut.String(hashedText)}'{KeepTimestamps("claimproc")} WHERE ClaimProcNum={POut.Long(claimProcNum)}";
					Db.NonQ(command);
				}
			}
		}

		#region Insurance subscribers and verifications (Open Dental 25.1 and 25.2 columns)
		///<summary>Rehashes inssub rows edited on or after GetHashingDate (SecDateTEdit). Hash: InsSubs.HashFields.</summary>
		private static void RunInsSub() {
			if(_areInsSubsUpdated) {
				return;
			}
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"inssub","SecurityHash")) {
				return;
			}
			ODEvent.Fire(ODEventType.ConvertDatabases,"Hashing insurance subscribers.");
			_areInsSubsUpdated=true;
			string command="SELECT * FROM inssub WHERE SecDateTEdit >= "+POut.Date(GetHashingDate());
			List<InsSub> listInsSubs=Crud.InsSubCrud.SelectMany(command);
			for(int i=0;i<listInsSubs.Count;i++) {
				string hashNew=InsSubs.HashFields(listInsSubs[i]);
				if(listInsSubs[i].SecurityHash!=hashNew) {
					WriteHash("inssub","InsSubNum",listInsSubs[i].InsSubNum,hashNew);
				}
			}
		}

		///<summary>Rehashes insverify rows edited on or after GetHashingDate (SecDateTEdit). Hash: InsVerifies.HashFields.</summary>
		private static void RunInsVerify() {
			if(_areInsVerifiesUpdated) {
				return;
			}
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"insverify","SecurityHash")) {
				return;
			}
			ODEvent.Fire(ODEventType.ConvertDatabases,"Hashing insurance verifications.");
			_areInsVerifiesUpdated=true;
			string command="SELECT * FROM insverify WHERE SecDateTEdit >= "+POut.Date(GetHashingDate());
			List<InsVerify> listInsVerifies=Crud.InsVerifyCrud.SelectMany(command);
			for(int i=0;i<listInsVerifies.Count;i++) {
				string hashNew=InsVerifies.HashFields(listInsVerifies[i]);
				if(listInsVerifies[i].SecurityHash!=hashNew) {
					WriteHash("insverify","InsVerifyNum",listInsVerifies[i].InsVerifyNum,hashNew);
				}
			}
		}

		///<summary>Rehashes insverifyhist rows edited on or after GetHashingDate (SecDateTEdit). Hash: InsVerifyHists.HashFields, which gives the
		///same value as the insverify row it was copied from.</summary>
		private static void RunInsVerifyHist() {
			if(_areInsVerifyHistsUpdated) {
				return;
			}
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"insverifyhist","SecurityHash")) {
				return;
			}
			ODEvent.Fire(ODEventType.ConvertDatabases,"Hashing insurance verification history.");
			_areInsVerifyHistsUpdated=true;
			string command="SELECT * FROM insverifyhist WHERE SecDateTEdit >= "+POut.Date(GetHashingDate());
			List<InsVerifyHist> listInsVerifyHists=Crud.InsVerifyHistCrud.SelectMany(command);
			for(int i=0;i<listInsVerifyHists.Count;i++) {
				string hashNew=InsVerifyHists.HashFields(listInsVerifyHists[i]);
				if(listInsVerifyHists[i].SecurityHash!=hashNew) {
					WriteHash("insverifyhist","InsVerifyHistNum",listInsVerifyHists[i].InsVerifyHistNum,hashNew);
				}
			}
		}

		///<summary>The table's ON UPDATE CURRENT_TIMESTAMP columns (DateTStamp, SecDateTEdit), per database and table.</summary>
		private static Dictionary<string,List<string>> _dictOnUpdateColumns=new Dictionary<string,List<string>>();
		private static object _lockOnUpdateColumns=new object();

		///<summary>",Col=Col" for each ON UPDATE CURRENT_TIMESTAMP column of the table, to append to an UPDATE that only writes SecurityHash. A hash is
		///not data anyone edited, so writing it must not move the row's audit timestamp (DateTStamp, SecDateTEdit). Without this, the rehash after
		///adopting an Open Dental database would stamp every patient and every recent appointment, claim, claim procedure, payment split and
		///insurance row with the adoption time (found by the M1-Z rehearsal, tools/AdoptionValidation). Only for the hard-coded table names in this
		///class. Called once per row, so the database name comes from the connection settings (no query) when they hold it.</summary>
		private static string KeepTimestamps(string tableName) {
			string database=DataConnectionBase.DataConnection.GetDatabaseName();
			if(string.IsNullOrEmpty(database)) {
				database=LargeTableHelper.GetCurrentDatabase();//Connected by connection string.
			}
			string key=database+"."+tableName;
			List<string> listColumns;
			lock(_lockOnUpdateColumns) {
				_dictOnUpdateColumns.TryGetValue(key,out listColumns);
			}
			if(listColumns==null) {
				string command="SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() "
					+"AND TABLE_NAME='"+POut.String(tableName)+"' AND EXTRA LIKE '%on update%' ORDER BY ORDINAL_POSITION";
				DataTable table=Db.GetTable(command);
				listColumns=table.Rows.Cast<DataRow>().Select(x => PIn.String(x[0].ToString())).ToList();
				lock(_lockOnUpdateColumns) {
					_dictOnUpdateColumns[key]=listColumns;
				}
			}
			return string.Concat(listColumns.Select(x => ",`"+x+"`=`"+x+"`"));
		}

		///<summary>Writes one SecurityHash value, keeping the row's audit timestamp. Only for the hard-coded table and key names above.</summary>
		private static void WriteHash(string tableName,string priKeyName,long priKey,string hash) {
			string command="UPDATE "+tableName+" SET SecurityHash='"+POut.String(hash)+"'"+KeepTimestamps(tableName)+" WHERE "+priKeyName+"="+POut.Long(priKey);
			Db.NonQ(command);
		}

		///<summary>Decides whether a row that the program is about to save gets a freshly computed hash, from the row as it is in the database. True when
		///the stored hash still verifies, was written by Open Dental (see IsHashValid), or is blank (never hashed). False when the stored hash is one of
		///ours and does not match the stored values: the row was changed outside the program. The program then keeps the old hash, even when the save
		///changes a hashed field, so a user's edit never hides the outside change; the warning stays until an administrator runs the rehash tool. This is
		///the rule Patients.Update follows.</summary>
		public static bool DoRehashOnSave(string hashStored,string hashComputedFromStoredValues) {
			if(string.IsNullOrEmpty(hashStored)) {
				return true;
			}
			return IsHashValid(hashStored,hashComputedFromStoredValues);
		}
		#endregion Insurance subscribers and verifications (Open Dental 25.1 and 25.2 columns)

		#region Whole patient table
		///<summary>The patient hash for a PatNum (the only hashed patient field), as Patients.HashFields computes it.</summary>
		public static string HashPatNum(long patNum) {
			try {
				return TamperHash.Compute(patNum.ToString(),isPatientTable:true);
			}
			catch(Exception ex) {
				ex.DoNothing();
				return ex.GetType().Name;
			}
		}

		///<summary>Counts the patient rows whose SecurityHash does not match the hash computed from the row. countLegacy is the part of the result whose
		///hash was written by Open Dental (it can't be checked here, see IsLegacyHash); countBlank the part that was never hashed. The rest no longer
		///match: they were changed outside the program. Reads the table in batches. Returns -1 when there is no patient.SecurityHash column.</summary>
		public static long CountPatientHashesNotMatching(out long countLegacy,out long countBlank) {
			countLegacy=0;
			countBlank=0;
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"patient","SecurityHash")) {
				return -1;
			}
			long count=0;
			long patNumLast=0;
			while(true) {
				DataTable table=GetPatientBatch(patNumLast);
				if(table.Rows.Count==0) {
					break;
				}
				for(int i=0;i<table.Rows.Count;i++) {
					long patNum=PIn.Long(table.Rows[i]["PatNum"].ToString());
					string hashStored=PIn.String(table.Rows[i]["SecurityHash"].ToString());
					patNumLast=patNum;
					if(hashStored==HashPatNum(patNum)) {
						continue;
					}
					count++;
					if(string.IsNullOrEmpty(hashStored)) {
						countBlank++;
					}
					else if(IsLegacyHash(hashStored)) {
						countLegacy++;
					}
				}
			}
			return count;
		}

		///<summary>Recomputes patient.SecurityHash for every patient row, in batches of PATIENT_BATCH_SIZE, and writes only the hashes that differ.
		///Unlike UpdateHashing, which only fills blank and Open Dental hashes, this also replaces a hash that no longer matches because the row was changed
		///outside the program, so it clears those integrity warnings. The patient's DateTStamp is kept, because no patient data changes.
		///actionProgress, if given, gets the number of rows read so far and the number of rows in the table after each batch.
		///Returns the number of rows whose hash was rewritten, or -1 when there is no patient.SecurityHash column. Not for use during database conversions.
		///</summary>
		public static long RehashPatientTable(Action<long,long> actionProgress=null) {
			if(!LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"patient","SecurityHash")) {
				return -1;
			}
			bool hasDateTStamp=LargeTableHelper.ColumnExists(LargeTableHelper.GetCurrentDatabase(),"patient","DateTStamp");
			long countTotal=PIn.Long(Db.GetCount("SELECT COUNT(*) FROM patient"));
			long countRead=0;
			long countChanged=0;
			long patNumLast=0;
			while(true) {
				DataTable table=GetPatientBatch(patNumLast);
				if(table.Rows.Count==0) {
					break;
				}
				StringBuilder stringBuilderCase=new StringBuilder();
				List<long> listPatNumsChanged=new List<long>();
				for(int i=0;i<table.Rows.Count;i++) {
					long patNum=PIn.Long(table.Rows[i]["PatNum"].ToString());
					string hashStored=PIn.String(table.Rows[i]["SecurityHash"].ToString());
					patNumLast=patNum;
					string hashNew=HashPatNum(patNum);
					if(hashStored==hashNew) {
						continue;
					}
					stringBuilderCase.Append(" WHEN "+POut.Long(patNum)+" THEN '"+POut.String(hashNew)+"'");
					listPatNumsChanged.Add(patNum);
				}
				if(listPatNumsChanged.Count>0) {
					string command="UPDATE patient SET SecurityHash=CASE PatNum"+stringBuilderCase+" END"
						+(hasDateTStamp ? ",DateTStamp=DateTStamp" : "")
						+" WHERE PatNum IN("+string.Join(",",listPatNumsChanged.Select(x => POut.Long(x)))+")";
					Db.NonQ(command);
					countChanged+=listPatNumsChanged.Count;
				}
				countRead+=table.Rows.Count;
				actionProgress?.Invoke(countRead,countTotal);
			}
			_arePatientsUpdated=true;//Nothing is left for PatientWorker to do in this process.
			return countChanged;
		}

		///<summary>The next PATIENT_BATCH_SIZE patients after patNumLast, in PatNum order: PatNum and SecurityHash only.</summary>
		private static DataTable GetPatientBatch(long patNumLast) {
			string command="SELECT PatNum,SecurityHash FROM patient WHERE PatNum>"+POut.Long(patNumLast)+" ORDER BY PatNum LIMIT "+PATIENT_BATCH_SIZE;
			return Db.GetTable(command);
		}
		#endregion Whole patient table

		/// <summary>Handles the disposing of HashAlgorithms during updates. /// </summary>
		private static void ThreadJoinHelper() {
			static void joinThreads() {
				_listHashThreads.ForEach(x => x?.Join());
				TamperHash.ReleaseProviders();
			}
			ThreadStart threadStart=new ThreadStart(joinThreads);
			Thread threadJoin=new Thread(threadStart);
			threadJoin.IsBackground=true;
			threadJoin.Start();
		}
	}
}
