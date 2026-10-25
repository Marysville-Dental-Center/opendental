// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SyntheticData.Infrastructure;

namespace AdoptionValidation {
	public enum CopyMethod {
		///<summary>mariadb-dump to a file, then the mariadb client into a new database: the backup and restore the runbook prescribes.</summary>
		Dump,
		///<summary>CREATE TABLE ... LIKE and INSERT ... SELECT on the same server. For machines without the client programs.</summary>
		Sql,
	}

	///<summary>Makes the copy that adoption runs on. With CopyMethod.Dump this is a real backup (mariadb-dump) and restore, timed separately, so the
	///rehearsal measures what the outage window needs. Only local servers. The restore only ever creates a new database; the SQL copy only writes
	///synth* databases.</summary>
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

		///<summary>Restores a backup file as a new database: the runbook's restore (AdoptionValidation restore, runbook steps 0.6 and 3.2) and the
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
					+"--databases or --all-databases dump). Nothing was restored. Make a single-database dump (runbook step 3.1) or restore a file-level "
					+"copy under a new folder name (runbook step 0.6). Found: "+string.Join("; ",listProblems));
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

		///<summary>Copies every table with CREATE TABLE ... LIKE and INSERT ... SELECT (generated columns are recomputed). Rows are inserted in primary
		///key order, so CHECKSUM TABLE gives the same values as on the source.</summary>
		public static void CopyWithSql(Db db,string databaseSource,string databaseTarget) {
			LocalOnly.EnsureSafeIdentifier(databaseSource);
			LocalOnly.EnsureWritableDatabaseName(databaseTarget);
			db.Execute("DROP DATABASE IF EXISTS `"+databaseTarget+"`");
			db.Execute("CREATE DATABASE `"+databaseTarget+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
			foreach(TableInfo table in SchemaInfo.Load(db,databaseSource).DictTables.Values) {
				db.Execute("CREATE TABLE `"+databaseTarget+"`.`"+table.Name+"` LIKE `"+databaseSource+"`.`"+table.Name+"`");
				string columns=string.Join(",",table.ListColumns.Where(x => !x.IsGenerated).Select(x => "`"+x.Name+"`"));
				db.Execute("INSERT INTO `"+databaseTarget+"`.`"+table.Name+"` ("+columns+") SELECT "+columns+" FROM `"+databaseSource+"`.`"+table.Name+"` ORDER BY 1");
			}
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
