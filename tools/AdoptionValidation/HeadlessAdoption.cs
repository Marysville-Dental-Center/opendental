// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;
using SyntheticData.Infrastructure;
using Db=SyntheticData.Infrastructure.Db;

namespace AdoptionValidation {
	///<summary>Adopts a restored copy without starting the program: Adoption.Preflight and Adoption.Run (what the Adopt Database window calls), the
	///fork migration with every step again, the schema verifier, and SecurityHash.RehashNow (what Database Maintenance, Tools, Security Hashes calls),
	///in that order. The rehearsal adopts its copy with this class, and runbook steps 4 and 5 run it through "AdoptionValidation adopt", so the office
	///runs exactly the sequence that was rehearsed.
	///
	///Why not the program itself: starting it and logging in writes to the database (the computer row and its heartbeat, the Database Maintenance
	///list, signals, login records). The post-adoption report (runbook step 6) must see only what adoption wrote, so it is taken before anyone starts
	///the program, as in the rehearsal.
	///
	///Running it again is safe and is how an interrupted run is finished: an unfinished adoption is finished, a finished one is skipped, and the
	///migration, the verifier and the rehash change nothing the second time (rehearsal checks I1 to I4).</summary>
	public static class HeadlessAdoption {
		public const string REHASH_LOG_TEXT="Security hashes recalculated after adoption by AdoptionValidation adopt (runbook step 5)";

		public class Result {
			public string Database;
			///<summary>Hash formats per table before anything ran.</summary>
			public List<HashCount> ListHashesStart=new List<HashCount>();
			public AdoptionPreflight Preflight;
			///<summary>True when the database was adopted before this run. Adoption is skipped; the migration, the verifier and the rehash run again.
			///</summary>
			public bool WasAlreadyAdopted;
			///<summary>Null when adoption was skipped.</summary>
			public AdoptionResult Adoption;
			public bool IsAdoptionFinished;
			public ForkMigrationResult Migration;
			public bool HasPendingMigrations;
			public SchemaVerifyResult Verify;
			public DateTime DateRehashWindow;
			public List<HashCount> ListHashesAfter=new List<HashCount>();
			public bool IsRehashLogged;
			public List<string> ListFailures=new List<string>();

			public bool Passed {
				get { return ListFailures.Count==0; }
			}

			///<summary>Every patient, and every row in the rehash window, carries a fork hash.</summary>
			public bool IsRehashComplete {
				get {
					HashCount patient=ListHashesAfter.FirstOrDefault(x => x.Table=="patient");
					return patient!=null && patient.Current==patient.Rows && ListHashesAfter.All(x => x.InWindowNotCurrent==0);
				}
			}
		}

		///<summary>Runs the steps on the database the business layer is connected to (BusinessLayer.Connect). step runs one named step; the
		///rehearsal times it with it. Throws when the preflight refuses the database or adoption fails; every other check is collected in
		///ListFailures.</summary>
		public static Result Run(string database,Action<string> log,Action<string,Action> step=null) {
			log=log??(x => { });
			step=step??((name,action) => action());
			if(!string.Equals(BusinessLayer.CurrentDatabase(),database,StringComparison.OrdinalIgnoreCase)) {
				throw new ApplicationException("The business layer is connected to "+BusinessLayer.CurrentDatabase()+", not "+database+".");
			}
			Result result=new Result { Database=database };
			result.ListHashesStart=BusinessLayer.CountHashes();
			step("Preflight",() => result.Preflight=Adoption.Preflight());
			log(result.Preflight.Message);
			if(result.Preflight.IsAlreadyAdopted) {
				result.WasAlreadyAdopted=true;
				log("Adoption is skipped. The fork migration, the schema verifier and the rehash run again; they change nothing on a finished adoption.");
			}
			else if(!result.Preflight.CanAdopt) {
				throw new ApplicationException(result.Preflight.Message);
			}
			else {
				step("Adopt (Adoption.Run, including its fork migration steps)",() => result.Adoption=Adoption.Run(log));
			}
			ForkDbState state=ForkVersions.ReadState();
			result.IsAdoptionFinished=state.IsForkMarked && state.IsAdoptionRecordFinished;
			if(!result.IsAdoptionFinished) {
				result.ListFailures.Add("The adoption record is not finished: "+(state.ForkAdoptedFrom??"no ForkAdoptedFrom")+".");
			}
			step("Fork migration (all steps again)",() => result.Migration=ForkMigrationRunner.Run(new ForkMigrationOptions { RunAll=true }));
			log("Fork migration, every step again: "+result.Migration.ListStepsRun.Count+" steps run, "+result.Migration.ListChanges.Count+" changes.");
			foreach(string change in result.Migration.ListChanges.Take(20)) {
				log("  "+change);
			}
			if(result.Migration.ListChanges.Count>0) {
				result.ListFailures.Add("The fork migration changed "+result.Migration.ListChanges.Count+" thing(s) on the adopted copy; on an Open Dental 25.3.48 "
					+"database it must find nothing to change.");
			}
			result.HasPendingMigrations=ForkVersionGuard.Evaluate(ForkVersions.ReadState()).HasPendingMigrations;
			if(result.HasPendingMigrations) {
				result.ListFailures.Add("Fork migration steps are still pending.");
			}
			step("Schema verifier",() => result.Verify=SchemaVerifier.VerifyDatabase(database));
			bool isSchemaMatch=result.Verify.IsMatch && result.Verify.ListDifferences.Count==0;
			log(isSchemaMatch
				? "The schema matches Open Dental "+SchemaVerifier.ReferenceVersion+" exactly ("+result.Verify.CountTablesActual+" of "+result.Verify.CountTablesExpected
					+" tables)."
				: "The schema differs from Open Dental "+SchemaVerifier.ReferenceVersion+": "+result.Verify.ListDifferences.Count+" difference(s), "
					+result.Verify.ListMismatches.Count+" mismatch(es): "+string.Join("; ",result.Verify.ListDifferences.Take(20).Select(x => x.Table+"."+x.Item)));
			if(!isSchemaMatch) {
				result.ListFailures.Add("The schema differs from Open Dental "+SchemaVerifier.ReferenceVersion+".");
			}
			result.DateRehashWindow=OpenDentBusiness.Misc.SecurityHash.GetHashingDate();
			log("Rehash: every patient, and the other tables' rows dated on or after "
				+result.DateRehashWindow.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)+".");
			step("Rehash (SecurityHash.RehashNow)",() => OpenDentBusiness.Misc.SecurityHash.RehashNow());
			result.ListHashesAfter=BusinessLayer.CountHashes();
			foreach(HashCount hashCount in result.ListHashesAfter) {
				log("  "+hashCount);
			}
			if(!result.IsRehashComplete) {
				result.ListFailures.Add("The rehash left a patient, or a row in the rehash window, without a fork hash.");
			}
			//Database Maintenance's Security Hashes tool logs the rehash too; this is the same entry for the headless run.
			try {
				SecurityLogs.MakeLogEntry(EnumPermType.SecurityAdmin,0,REHASH_LOG_TEXT+": "
					+string.Join(", ",result.ListHashesAfter.Select(x => x.Table+" "+x.Current))+" rows with a fork hash.");
				result.IsRehashLogged=true;
			}
			catch(Exception ex) {
				log("Warning: the security log entry for the rehash could not be written: "+ex.Message);
			}
			log("Rehash finished.");
			return result;
		}

		///<summary>What "AdoptionValidation adopt" checks before it changes anything: the copy and the original are two different local databases,
		///nobody else is connected to either, the original is an unadopted Open Dental database on the allowlist, and a copy that has not been
		///adopted yet has the original's tables and exactly its row counts (runbook step 3.3 compared them in full) and was created after the
		///original (not the names swapped). Throws RefusedException.</summary>
		public static void CheckCopy(Db db,string database,string original,Action<string> log) {
			LocalOnly.EnsureSafeIdentifier(database);
			LocalOnly.EnsureSafeIdentifier(original);
			if(string.Equals(database,original,StringComparison.OrdinalIgnoreCase)) {
				throw new RefusedException("--database and --original are the same ("+database+"). Adopt the restored copy, never the original.");
			}
			foreach(string name in new[] { original,database }) {
				if(!db.DatabaseExists(name)) {
					throw new RefusedException("Database "+name+" does not exist on this server.");
				}
			}
			List<string> listConnections=OtherConnections(db,new[] { database,original });
			if(listConnections.Count>0) {
				throw new RefusedException("Other connections use these databases; close every program connected to them first (runbook step 1.1): "
					+string.Join("; ",listConnections));
			}
			ForkDbState stateOriginal=ReadState(db,original);
			if(stateOriginal.IsForkMarked || stateOriginal.ForkAdoptedFrom!=null || !ForkVersions.IsAdoptable(ForkVersions.ParseVersion(stateOriginal.DataBaseVersion))) {
				throw new RefusedException("--original "+original+" is not an unadopted Open Dental database on the allowlist (DataBaseVersion "
					+(stateOriginal.DataBaseVersion??"missing")+(stateOriginal.ForkAdoptedFrom==null ? "" : ", ForkAdoptedFrom "+stateOriginal.ForkAdoptedFrom)
					+"). It must name the Open Dental database the copy was restored from.");
			}
			ForkDbState stateCopy=ReadState(db,database);
			if(stateCopy.IsForkMarked || stateCopy.ForkAdoptedFrom!=null) {
				log("The copy "+database+" already has an adoption record ("+(stateCopy.ForkAdoptedFrom??"ForkSchemaLevel "+stateCopy.ForkSchemaLevel)
					+"): this run finishes or repeats it, and the copy is not compared with "+original+" again.");
				return;
			}
			List<string> listDiffs=CompareRowCounts(db,original,database);
			if(listDiffs.Count>0) {
				throw new RefusedException("The copy "+database+" is not an exact copy of "+original+" (tables or row counts differ: "
					+string.Join("; ",listDiffs.Take(10))+(listDiffs.Count>10 ? "; and "+(listDiffs.Count-10)+" more" : "")
					+"). Restore the copy again (runbook step 3.2) and compare (step 3.3).");
			}
			log("The copy "+database+" has the same "+TableNames(db,original).Count+" tables and row counts as "+original+".");
			//Two identical databases could still be named the wrong way round. The copy was restored after the original's tables were created, so its
			//oldest table is younger than the original's oldest one.
			DateTime? dateCopyOldest=OldestTableCreated(db,database);
			DateTime? dateOriginalOldest=OldestTableCreated(db,original);
			if(dateCopyOldest!=null && dateOriginalOldest!=null) {
				if(dateCopyOldest<=dateOriginalOldest) {
					throw new RefusedException("The tables of "+database+" (oldest created "+Format(dateCopyOldest)+") are not newer than those of "+original
						+" (oldest created "+Format(dateOriginalOldest)+"): "+database+" does not look like the copy restored from "+original
						+". Are --database and --original the wrong way round?");
				}
				log("The copy's tables were created "+Format(dateCopyOldest)+" or later, after the original's ("+Format(dateOriginalOldest)+").");
			}
		}

		private static DateTime? OldestTableCreated(Db db,string database) {
			object value=db.Scalar("SELECT MIN(CREATE_TIME) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_TYPE='BASE TABLE'");
			return value==null ? (DateTime?)null : Convert.ToDateTime(value,CultureInfo.InvariantCulture);
		}

		private static string Format(DateTime? date) {
			return date?.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)??"";
		}

		///<summary>Connections other than this one whose default database is one of the given databases.</summary>
		public static List<string> OtherConnections(Db db,IEnumerable<string> listDatabases) {
			//This process's own idle pooled connections would count too; there are none in the tool, but the tests share a process. The server may
			//take a moment to remove a closed connection, so a non-empty list is read again a few times before it counts.
			MySqlConnector.MySqlConnection.ClearAllPools();
			string inList=string.Join(",",listDatabases.Select(x => SqlLiteral.Quote(x)));
			List<string> listConnections=new List<string>();
			for(int attempt=0;attempt<5;attempt++) {
				if(attempt>0) {
					System.Threading.Thread.Sleep(200);
				}
				listConnections=db.Rows("SELECT ID,USER,HOST,DB,COMMAND FROM information_schema.PROCESSLIST WHERE DB IN ("+inList+") AND ID<>CONNECTION_ID() "
					+"ORDER BY ID").Select(x => "connection "+x[0]+" by "+x[1]+" from "+x[2]+" to "+x[3]+" ("+x[4]+")").ToList();
				if(listConnections.Count==0) {
					break;
				}
			}
			return listConnections;
		}

		///<summary>The version preferences and fork markers of any local database, read with plain SQL.</summary>
		public static ForkDbState ReadState(Db db,string database) {
			LocalOnly.EnsureSafeIdentifier(database);
			ForkDbState state=new ForkDbState();
			if(db.ScalarLong("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_NAME='preference'")==0) {
				return state;
			}
			Dictionary<string,string> dictValues=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
			foreach(object[] row in db.Rows("SELECT PrefName,ValueString FROM `"+database+"`.preference WHERE PrefName IN ('DataBaseVersion','ProgramVersion','"
				+ForkVersions.PREF_FORK_SCHEMA_LEVEL+"','"+ForkVersions.PREF_OD_SCHEMA_COMPAT+"','"+ForkVersions.PREF_FORK_ADOPTED_FROM+"') ORDER BY PrefNum"))
			{
				string name=Convert.ToString(row[0]);
				if(!dictValues.ContainsKey(name)) {
					dictValues[name]=Convert.ToString(row[1]);
				}
			}
			state.DataBaseVersion=dictValues.TryGetValue("DataBaseVersion",out string value) ? value : null;
			state.ProgramVersion=dictValues.TryGetValue("ProgramVersion",out value) ? value : null;
			state.ForkSchemaLevel=dictValues.TryGetValue(ForkVersions.PREF_FORK_SCHEMA_LEVEL,out value) ? value : null;
			state.OdSchemaCompat=dictValues.TryGetValue(ForkVersions.PREF_OD_SCHEMA_COMPAT,out value) ? value : null;
			state.ForkAdoptedFrom=dictValues.TryGetValue(ForkVersions.PREF_FORK_ADOPTED_FROM,out value) ? value : null;
			return state;
		}

		///<summary>Tables only in one database, and tables whose exact row counts (COUNT(*)) differ.</summary>
		public static List<string> CompareRowCounts(Db db,string databaseA,string databaseB) {
			List<string> listA=TableNames(db,databaseA);
			List<string> listB=TableNames(db,databaseB);
			List<string> listDiffs=new List<string>();
			listDiffs.AddRange(listA.Except(listB,StringComparer.OrdinalIgnoreCase).Select(x => x+" only in "+databaseA));
			listDiffs.AddRange(listB.Except(listA,StringComparer.OrdinalIgnoreCase).Select(x => x+" only in "+databaseB));
			foreach(string table in listA.Intersect(listB,StringComparer.OrdinalIgnoreCase)) {
				long countA=db.ScalarLong("SELECT COUNT(*) FROM `"+databaseA+"`.`"+table+"`");
				long countB=db.ScalarLong("SELECT COUNT(*) FROM `"+databaseB+"`.`"+table+"`");
				if(countA!=countB) {
					listDiffs.Add(table+" "+countA+" rows in "+databaseA+", "+countB+" in "+databaseB);
				}
			}
			return listDiffs;
		}

		private static List<string> TableNames(Db db,string database) {
			return db.Rows("SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA="+SqlLiteral.Quote(database)+" AND TABLE_TYPE='BASE TABLE' "
				+"ORDER BY TABLE_NAME").Select(x => Convert.ToString(x[0])).ToList();
		}
	}
}
