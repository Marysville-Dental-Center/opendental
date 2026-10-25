// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	public enum CopyMethod {
		///<summary>The runbook's copy (step 3.2, AdoptionValidation copy): CREATE TABLE ... LIKE and INSERT ... SELECT on the database server itself
		///(CopyOnServer). Exact, FLOAT values included, and no row leaves the server.</summary>
		Server,
		///<summary>mariadb-dump to a file, then the runbook's restore into a new database. Rounds FLOAT values with more than 6 significant digits
		///(FloatCheck); kept to rehearse a logical backup.</summary>
		Dump,
	}

	///<summary>Makes the copy that adoption runs on. With CopyMethod.Server it is the runbook's server-side copy (CopyOnServer); with CopyMethod.Dump a
	///real backup (mariadb-dump) and restore, timed separately. Only local servers. Both only ever create a new database.</summary>
	public static class DatabaseCopier {
		///<summary>Folders searched for mariadb-dump.exe and mariadb.exe (or mysqldump.exe and mysql.exe), after the MARIADB_BIN environment
		///variable and the PATH.</summary>
		public static IEnumerable<string> CandidateBinFolders() {
			string fromEnv=Environment.GetEnvironmentVariable("MARIADB_BIN");
			if(!string.IsNullOrWhiteSpace(fromEnv)) {
				yield return fromEnv;
			}
			foreach(string folder in (Environment.GetEnvironmentVariable("PATH")??"").Split(';').Where(x => x.Trim()!="")) {
				yield return folder.Trim();
			}
			foreach(string root in new[] { Environment.GetEnvironmentVariable("ProgramFiles"),Environment.GetEnvironmentVariable("ProgramW6432") }) {
				if(string.IsNullOrEmpty(root) || !Directory.Exists(root)) {
					continue;
				}
				foreach(string dir in Directory.GetDirectories(root).Where(x => Path.GetFileName(x).StartsWith("MariaDB",StringComparison.OrdinalIgnoreCase)
					|| Path.GetFileName(x).StartsWith("MySQL",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x => x))
				{
					yield return Path.Combine(dir,"bin");
					foreach(string sub in Directory.GetDirectories(dir)) {
						yield return Path.Combine(sub,"bin");
					}
				}
			}
		}

		///<summary>The dump and client programs, or null when they are not installed.</summary>
		public static (string dump,string client)? FindClientTools() {
			foreach(string folder in CandidateBinFolders()) {
				try {
					foreach((string dump,string client) pair in new[] { ("mariadb-dump.exe","mariadb.exe"),("mysqldump.exe","mysql.exe") }) {
						string pathDump=Path.Combine(folder,pair.dump);
						string pathClient=Path.Combine(folder,pair.client);
						if(File.Exists(pathDump) && File.Exists(pathClient)) {
							return (pathDump,pathClient);
						}
					}
				}
				catch(ArgumentException) {
					//A malformed PATH entry.
				}
			}
			return null;
		}

		///<summary>Writes a full logical backup of the database: every table, with routines, triggers and events, binary columns as hex, UTC
		///timestamps. MyISAM has no transactions, so the tables are locked for the dump (--lock-tables); the office does this with every Open Dental
		///client and service stopped.</summary>
		public static void Backup(ServerSettings server,string database,string pathFile,string pathDump) {
			LocalOnly.EnsureLocalHost(server.Server);
			LocalOnly.EnsureSafeIdentifier(database);
			Run(pathDump,ConnectionArgs(server)+" --lock-tables --routines --triggers --events --hex-blob --default-character-set=utf8mb4 "
				+"--result-file=\""+pathFile+"\" "+database,null,server);
		}

		///<summary>Restores a backup file as a new database: AdoptionValidation restore and the
		///rehearsal's. It never writes to a database that exists already, and it refuses a file that names a database (DumpCheck): a --databases or
		///--all-databases dump would otherwise replay its DROP TABLE, CREATE TABLE and INSERT statements into the database it names, the live one.
		///The client also runs with --one-database, so any USE that got past the check would only make it skip statements. Then it creates the
		///database (utf8mb3, as Open Dental creates it) and loads the file into it. On a failure the partial database is left for inspection.
		///</summary>
		public static void Restore(ServerSettings server,string pathFile,string database,string pathClient) {
			LocalOnly.EnsureLocalHost(server.Server);
			LocalOnly.EnsureSafeIdentifier(database);
			if(!File.Exists(pathFile)) {
				throw new RefusedException("The backup file "+pathFile+" does not exist.");
			}
			using(Db db=Db.Open(server)) {
				if(db.DatabaseExists(database)) {
					throw new RefusedException("Database "+database+" already exists. The restore only creates a new database; choose a name that does not "
						+"exist yet.");
				}
			}
			List<DumpCheck.Problem> listProblems=DumpCheck.Scan(pathFile);
			if(listProblems.Count>0) {
				throw new RefusedException("The backup file "+pathFile+" names a database, so loading it would also write to that database (it may be a "
					+"--databases or --all-databases dump). Nothing was restored. Make a single-database dump. Found: "+string.Join("; ",listProblems));
			}
			using(Db db=Db.Open(server)) {
				//No IF NOT EXISTS: if the name was taken since the check above, this fails instead of loading into it.
				db.Execute("CREATE DATABASE `"+database+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			}
			try {
				Run(pathClient,ConnectionArgs(server)+" --default-character-set=utf8mb4 --one-database "+database,pathFile,server);
			}
			catch(ApplicationException ex) {
				throw new ApplicationException(ex.Message+" The new database "+database+" holds a partial copy; drop it (DROP DATABASE `"+database+"`;) "
					+"before trying again.",ex);
			}
		}

		///<summary>The runbook's copy (step 3.2, AdoptionValidation copy, and the dry run): copies every table of original into the new database target
		///on the same server, with CREATE TABLE ... LIKE (the same definition, indexes and table options), the original's AUTO_INCREMENT counter, and
		///INSERT ... SELECT of every stored column in primary key order (generated columns are computed again). The server copies the stored values
		///themselves, so the copy is exact where a dump is not (FLOAT values, FloatCheck), and no row leaves the server. Before it creates anything it
		///refuses (RefusedException): the same name twice, a target that exists, an original that does not, any connection to either (the account must
		///see every connection), and views, triggers, routines or events in the original, which this copy would not carry. Afterwards every table's
		///row count and AUTO_INCREMENT must equal the original's and nobody may have connected meanwhile; otherwise it throws and leaves the partial
		///copy, to be dropped. Returns the number of rows copied.</summary>
		public static long CopyOnServer(Db db,string original,string target,Action<string> log) {
			log=log??(x => { });
			LocalOnly.EnsureSafeIdentifier(original);
			LocalOnly.EnsureSafeIdentifier(target);
			if(string.Equals(original,target,StringComparison.OrdinalIgnoreCase)) {
				throw new RefusedException("--original and --database are the same ("+target+"). The copy is a new database.");
			}
			if(!db.DatabaseExists(original)) {
				throw new RefusedException("Database "+original+" does not exist on this server.");
			}
			if(db.DatabaseExists(target)) {
				throw new RefusedException("Database "+target+" already exists. The copy only creates a new database; choose a name that does not exist yet.");
			}
			List<string> listConnections=HeadlessAdoption.OtherConnections(db,new[] { original,target });
			if(listConnections.Count>0) {
				throw new RefusedException("Other connections use these databases; close every program connected to them first (runbook step 1.1): "
					+string.Join("; ",listConnections));
			}
			string quoted=SqlLiteral.Quote(original);
			List<string> listNotCopied=new List<string>();
			listNotCopied.AddRange(db.Rows("SELECT TABLE_NAME,TABLE_TYPE FROM information_schema.TABLES WHERE TABLE_SCHEMA="+quoted+" AND TABLE_TYPE<>'BASE TABLE'")
				.Select(x => Convert.ToString(x[1]).ToLowerInvariant()+" "+x[0]));
			//EVENT_OBJECT_SCHEMA, not TRIGGER_SCHEMA: the server looks up only that database's tables for it, while TRIGGER_SCHEMA makes it open every
			//table of every database (and wait for any other session's DDL). A trigger is always in its table's database.
			listNotCopied.AddRange(db.Rows("SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE EVENT_OBJECT_SCHEMA="+quoted).Select(x => "trigger "+x[0]));
			listNotCopied.AddRange(db.Rows("SELECT ROUTINE_TYPE,ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA="+quoted)
				.Select(x => Convert.ToString(x[0]).ToLowerInvariant()+" "+x[1]));
			listNotCopied.AddRange(db.Rows("SELECT EVENT_NAME FROM information_schema.EVENTS WHERE EVENT_SCHEMA="+quoted).Select(x => "event "+x[0]));
			if(listNotCopied.Count>0) {
				throw new RefusedException(original+" has objects this copy does not carry: "+string.Join(", ",listNotCopied.Take(20))
					+(listNotCopied.Count>20 ? " and "+(listNotCopied.Count-20)+" more" : "")+". Nothing was copied; give this list to the developers.");
			}
			object[] rowCharset=db.Rows("SELECT DEFAULT_CHARACTER_SET_NAME,DEFAULT_COLLATION_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME="+quoted)[0];
			string charset=Convert.ToString(rowCharset[0]);
			string collation=Convert.ToString(rowCharset[1]);
			LocalOnly.EnsureSafeIdentifier(charset);
			LocalOnly.EnsureSafeIdentifier(collation);
			List<(string name,long? autoIncrement)> listTables=db.Rows("SELECT TABLE_NAME,AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA="+quoted
				+" AND TABLE_TYPE='BASE TABLE' ORDER BY TABLE_NAME")
				.Select(x => (Convert.ToString(x[0]),x[1]==null ? (long?)null : Convert.ToInt64(x[1],CultureInfo.InvariantCulture))).ToList();
			foreach((string name,long? autoIncrement) table in listTables) {
				if(table.name.Contains("`")) {
					throw new RefusedException("The table name "+table.name+" cannot be copied.");
				}
			}
			//Every table's stored columns and primary key, read once (information_schema is slow to ask table by table).
			Dictionary<string,List<string>> dictColumns=new Dictionary<string,List<string>>(StringComparer.Ordinal);
			Dictionary<string,string> dictAutoIncrementColumn=new Dictionary<string,string>(StringComparer.Ordinal);
			foreach(object[] row in db.Rows("SELECT TABLE_NAME,COLUMN_NAME,EXTRA FROM information_schema.COLUMNS WHERE TABLE_SCHEMA="+quoted
				+" ORDER BY TABLE_NAME,ORDINAL_POSITION"))
			{
				string extra=Convert.ToString(row[2]??"").ToLowerInvariant();
				if(extra.Contains("generated")) {
					continue;//Computed again from the stored columns.
				}
				string tableName=Convert.ToString(row[0]);
				if(extra.Contains("auto_increment")) {
					dictAutoIncrementColumn[tableName]="`"+Convert.ToString(row[1]).Replace("`","``")+"`";
				}
				if(!dictColumns.TryGetValue(tableName,out List<string> list)) {
					dictColumns[tableName]=list=new List<string>();
				}
				list.Add("`"+Convert.ToString(row[1]).Replace("`","``")+"`");
			}
			Dictionary<string,List<string>> dictKeys=new Dictionary<string,List<string>>(StringComparer.Ordinal);
			foreach(object[] row in db.Rows("SELECT TABLE_NAME,COLUMN_NAME FROM information_schema.STATISTICS WHERE TABLE_SCHEMA="+quoted
				+" AND INDEX_NAME='PRIMARY' ORDER BY TABLE_NAME,SEQ_IN_INDEX"))
			{
				string tableName=Convert.ToString(row[0]);
				if(!dictKeys.TryGetValue(tableName,out List<string> list)) {
					dictKeys[tableName]=list=new List<string>();
				}
				list.Add("`"+Convert.ToString(row[1]).Replace("`","``")+"`");
			}
			log("Copying "+listTables.Count+" tables from "+original+" to the new database "+target+" ("+charset+", "+collation+") on the server.");
			//No IF NOT EXISTS: if the name was taken since the check above, this fails instead of writing into it.
			db.Execute("CREATE DATABASE `"+target+"` CHARACTER SET "+charset+" COLLATE "+collation);
			long countRows=0;
			Stopwatch stopwatch=Stopwatch.StartNew();
			try {
				foreach((string name,long? autoIncrement) table in listTables) {
					string source="`"+original+"`.`"+table.name+"`";
					string copy="`"+target+"`.`"+table.name+"`";
					db.Execute("CREATE TABLE "+copy+" LIKE "+source);
					//CREATE TABLE ... LIKE starts the counter at 1, and the inserts take it to the largest value plus one. Where the original's counter is
					//higher (rows were deleted at the end), it is set first, while the copy is empty. The check below compares every counter.
					if(table.autoIncrement!=null) {
						bool isSetNeeded=true;
						if(dictAutoIncrementColumn.TryGetValue(table.name,out string columnAuto)) {
							long max=db.ScalarLong("SELECT IFNULL(MAX("+columnAuto+"),0) FROM "+source);
							isSetNeeded=table.autoIncrement.Value>max+1;
						}
						if(isSetNeeded) {
							db.Execute("ALTER TABLE "+copy+" AUTO_INCREMENT="+table.autoIncrement.Value.ToString(CultureInfo.InvariantCulture));
						}
					}
					string columns=string.Join(",",dictColumns[table.name]);
					string orderBy=dictKeys.TryGetValue(table.name,out List<string> listKey) ? string.Join(",",listKey) : columns;
					countRows+=db.Execute("INSERT INTO "+copy+" ("+columns+") SELECT "+columns+" FROM "+source+" ORDER BY "+orderBy);
				}
			}
			catch(Exception ex) {
				throw new ApplicationException("The copy stopped: "+ex.Message+" The new database "+target+" holds a partial copy; drop it (DROP DATABASE `"+target
					+"`;) before trying again.",ex);
			}
			List<string> listProblems=HeadlessAdoption.CompareRowCounts(db,original,target);
			Dictionary<string,long?> dictAutoIncrementCopy=db.Rows("SELECT TABLE_NAME,AUTO_INCREMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA="
				+SqlLiteral.Quote(target)+" AND TABLE_TYPE='BASE TABLE'").ToDictionary(x => Convert.ToString(x[0]),
				x => x[1]==null ? (long?)null : Convert.ToInt64(x[1],CultureInfo.InvariantCulture),StringComparer.Ordinal);
			foreach((string name,long? autoIncrement) table in listTables.Where(x => x.autoIncrement!=null)) {
				dictAutoIncrementCopy.TryGetValue(table.name,out long? autoIncrementCopy);
				if(autoIncrementCopy!=table.autoIncrement) {
					listProblems.Add(table.name+" AUTO_INCREMENT "+table.autoIncrement+" in "+original+", "+(autoIncrementCopy?.ToString()??"none")+" in "+target);
				}
			}
			listConnections=HeadlessAdoption.OtherConnections(db,new[] { original,target });
			if(listConnections.Count>0) {
				listProblems.Add("someone connected during the copy: "+string.Join("; ",listConnections));
			}
			if(listProblems.Count>0) {
				throw new ApplicationException("The copy does not match "+original+": "+string.Join("; ",listProblems.Take(10))+(listProblems.Count>10 ? "; and "
					+(listProblems.Count-10)+" more" : "")+". Drop "+target+" (DROP DATABASE `"+target+"`;) and copy again when nobody is connected.");
			}
			log("Copied "+listTables.Count+" tables, "+countRows.ToString("N0",CultureInfo.InvariantCulture)+" rows, in "
				+stopwatch.Elapsed.TotalSeconds.ToString("0.0",CultureInfo.InvariantCulture)+" s; every table's row count and AUTO_INCREMENT equal "+original+"'s.");
			return countRows;
		}

		private static string ConnectionArgs(ServerSettings server) {
			//The password, if any, goes through the MYSQL_PWD environment variable (set in Run), never the command line.
			return "--host="+server.Server+" --port="+server.Port+" --user="+server.User+" --protocol=TCP";
		}

		private static void Run(string fileName,string arguments,string pathStdIn,ServerSettings server) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo(fileName,arguments) {
				UseShellExecute=false,
				CreateNoWindow=true,
				RedirectStandardError=true,
				RedirectStandardOutput=true,
				RedirectStandardInput=pathStdIn!=null,
			};
			if(!string.IsNullOrEmpty(server.Password)) {
				processStartInfo.EnvironmentVariables["MYSQL_PWD"]=server.Password;
			}
			using(Process process=Process.Start(processStartInfo)) {
				System.Threading.Tasks.Task<string> taskErr=process.StandardError.ReadToEndAsync();
				System.Threading.Tasks.Task<string> taskOut=process.StandardOutput.ReadToEndAsync();
				if(pathStdIn!=null) {
					using(FileStream fileStream=File.OpenRead(pathStdIn)) {
						fileStream.CopyTo(process.StandardInput.BaseStream);
					}
					process.StandardInput.Close();
				}
				process.WaitForExit();
				string error=taskErr.Result;
				taskOut.Wait();
				if(process.ExitCode!=0) {
					throw new ApplicationException(Path.GetFileName(fileName)+" failed with exit code "+process.ExitCode+": "+error.Trim());
				}
			}
		}
	}
}
