using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using CodeBase;
using DataConnectionBase;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>Runs the fork's own database migrations, which take over from the frozen 24.3.63 ConvertDatabases chain. Steps are classes derived
	///from ForkMigrationStep and marked with ForkMigrationAttribute, each in its own file; the runner finds them by reflection and runs them in level
	///order. Its state is kept in preference rows (data only, no schema change):
	///- ForkMigrationsApplied: comma-separated levels of the steps that finished on this database. A step merged later with a lower level than an
	///  applied one still runs, because the runner checks each level, not just the highest.
	///- ForkSchemaLevel: the highest applied level (the "ForkSchemaLevel" marker of the version strategy, docs/specs/port-backlog.md, risk R1).
	///- ForkMigrationInProgress: the level of a step that started and has not finished. MyISAM has no transactions, so a step that was interrupted
	///  can leave the database half changed. While this is set the runner refuses to run; restore the backup taken before the migration.
	///None of these names is an Open Dental preference name.
	///Version checks, backups, the CorruptedDatabase flag and the progress window are the caller's job (startup and the adoption tool).</summary>
	public static class ForkMigrationRunner {
		public const string PrefNameApplied="ForkMigrationsApplied";
		public const string PrefNameSchemaLevel="ForkSchemaLevel";
		public const string PrefNameInProgress="ForkMigrationInProgress";

		///<summary>All steps in this assembly, ordered by level. Throws if a step has no attribute, an invalid level, or a level another step uses.</summary>
		public static List<ForkMigrationStep> GetSteps() {
			return GetSteps(typeof(ForkMigrationRunner).Assembly);
		}

		///<summary>All steps in the given assemblies, ordered by level. Validated like GetSteps().</summary>
		public static List<ForkMigrationStep> GetSteps(params Assembly[] arrayAssemblies) {
			List<ForkMigrationStep> listSteps=new List<ForkMigrationStep>();
			foreach(Assembly assembly in arrayAssemblies) {
				foreach(Type type in assembly.GetTypes()) {
					if(type.IsAbstract || !typeof(ForkMigrationStep).IsAssignableFrom(type)) {
						continue;
					}
					if(type.GetConstructor(Type.EmptyTypes)==null) {
						throw new ApplicationException("Fork migration step "+type.FullName+" needs a public parameterless constructor.");
					}
					ForkMigrationStep step=(ForkMigrationStep)Activator.CreateInstance(type);
					ValidateStep(step);
					listSteps.Add(step);
				}
			}
			ValidateLevelsUnique(listSteps);
			return listSteps.OrderBy(x => x.Level).ToList();
		}

		///<summary>Throws if two steps share a level.</summary>
		public static void ValidateLevelsUnique(List<ForkMigrationStep> listSteps) {
			List<IGrouping<long,ForkMigrationStep>> listDuplicates=listSteps.GroupBy(x => x.Level).Where(x => x.Count()>1).ToList();
			if(listDuplicates.Count>0) {
				throw new ApplicationException("Fork migration levels must be unique: "
					+string.Join("; ",listDuplicates.Select(x => x.Key+" is used by "+string.Join(", ",x.Select(y => y.GetType().Name)))));
			}
		}

		///<summary>Throws if the step has no attribute, a level that is not yyyyMMddNN (from 2026 on), or no name.</summary>
		public static void ValidateStep(ForkMigrationStep step) {
			ForkMigrationAttribute attribute=step.Attribute;//Throws when missing.
			string level=attribute.Level.ToString();
			DateTime date;
			if(level.Length!=10 || !DateTime.TryParseExact(level.Substring(0,8),"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,
				System.Globalization.DateTimeStyles.None,out date) || date.Year<2026)
			{
				throw new ApplicationException("Fork migration step "+step.GetType().FullName+" has level "+level+"; levels are yyyyMMddNN, from 2026 on.");
			}
			if(string.IsNullOrWhiteSpace(attribute.Name)) {
				throw new ApplicationException("Fork migration step "+step.GetType().FullName+" has no name.");
			}
		}

		///<summary>Levels of the steps that finished on the connected database.</summary>
		public static HashSet<long> GetAppliedLevels() {
			string value=GetPrefRaw(PrefNameApplied);
			HashSet<long> setLevels=new HashSet<long>();
			if(string.IsNullOrWhiteSpace(value)) {
				return setLevels;
			}
			foreach(string part in value.Split(',')) {
				long level;
				if(long.TryParse(part.Trim(),out level)) {
					setLevels.Add(level);
				}
			}
			return setLevels;
		}

		///<summary>The steps that have not finished on the connected database.</summary>
		public static List<ForkMigrationStep> GetPendingSteps() {
			HashSet<long> setApplied=GetAppliedLevels();
			return GetSteps().Where(x => !setApplied.Contains(x.Level)).ToList();
		}

		///<summary>Runs every step that has not finished on the connected database, or every step when options.RunAll is set (each step is idempotent,
		///so a database already in the target state stays unchanged). Throws if a previous run was interrupted, or if a step throws; the step that
		///failed stays marked as in progress.</summary>
		public static ForkMigrationResult Run(ForkMigrationOptions options=null) {
			options=options??new ForkMigrationOptions();
			if(RemotingClient.MiddleTierRole==MiddleTierRole.ClientMT) {
				throw new ApplicationException("Fork migrations must run with a direct database connection, not through the middle tier.");
			}
			string databaseName=Db.GetScalar("SELECT DATABASE()");
			if(string.IsNullOrEmpty(databaseName)) {
				throw new ApplicationException("No database is selected.");
			}
			if(Db.GetScalar("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA='"+POut.String(databaseName)+"' "
				+"AND TABLE_NAME='preference'")=="0")
			{
				throw new ApplicationException("Database "+databaseName+" has no preference table; it is not an Open Dental database.");
			}
			string inProgress=GetPrefRaw(PrefNameInProgress);
			if(!string.IsNullOrEmpty(inProgress)) {
				throw new ApplicationException("Fork migration step "+inProgress+" was interrupted on database "+databaseName+". The database may be "
					+"partly changed. Restore the backup taken before the migration.");
			}
			List<ForkMigrationStep> listSteps=options.ListSteps??GetSteps();
			HashSet<long> setApplied=GetAppliedLevels();
			ForkMigrationContext context=new ForkMigrationContext(databaseName);
			ForkMigrationResult result=new ForkMigrationResult();
			result.DatabaseName=databaseName;
			int commandTimeoutOld=DataConnection.CommandTimeout;
			DataConnection.CommandTimeout=43200;//12 hours, like ConvertDatabases: an ALTER on a large table can take long.
			try {
				foreach(ForkMigrationStep step in listSteps) {
					bool isApplied=setApplied.Contains(step.Level);
					if(isApplied && !options.RunAll) {
						result.ListStepsSkipped.Add(step);
						continue;
					}
					ODEvent.Fire(ODEventType.ConvertDatabases,"Upgrading database: "+step.Name);
					SetPrefRaw(PrefNameInProgress,step.Level.ToString());
					context.StepCur=step;
					int countChangesBefore=context.ListChanges.Count;
					step.Apply(context);
					context.StepCur=null;
					if(context.ListChanges.Count>countChangesBefore) {
						result.ListStepsChanged.Add(step);
					}
					result.ListStepsRun.Add(step);
					setApplied.Add(step.Level);
					SetPrefRaw(PrefNameApplied,string.Join(",",setApplied.OrderBy(x => x)));
					SetPrefRaw(PrefNameSchemaLevel,setApplied.Max().ToString());
					SetPrefRaw(PrefNameInProgress,"");
				}
			}
			finally {
				DataConnection.CommandTimeout=commandTimeoutOld;
			}
			result.ListChanges=context.ListChanges;
			return result;
		}

		///<summary>Reads a preference row straight from the table, bypassing the cache. Null if there is no row.</summary>
		private static string GetPrefRaw(string prefName) {
			DataTable table=Db.GetTable("SELECT ValueString FROM preference WHERE PrefName='"+POut.String(prefName)+"'");
			if(table.Rows.Count==0) {
				return null;
			}
			return table.Rows[0][0].ToString();
		}

		///<summary>Writes a preference row straight to the table, inserting it if missing. These rows are not in the PrefName enum on purpose: they
		///belong to the runner, and the preference cache keeps unknown names without complaint.</summary>
		private static void SetPrefRaw(string prefName,string value) {
			string command="SELECT COUNT(*) FROM preference WHERE PrefName='"+POut.String(prefName)+"'";
			if(Db.GetScalar(command)=="0") {
				Db.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES ('"+POut.String(prefName)+"','"+POut.String(value)+"')");
				return;
			}
			Db.NonQ("UPDATE preference SET ValueString='"+POut.String(value)+"' WHERE PrefName='"+POut.String(prefName)+"'");
		}
	}

	public class ForkMigrationOptions {
		///<summary>Run steps that already finished too. Every step is idempotent, so this only changes a database that drifted. Used by the adoption
		///tool on a database without fork markers, and by tests.</summary>
		public bool RunAll;
		///<summary>Run only these steps (in this order) instead of every step in the assembly. For tests.</summary>
		public List<ForkMigrationStep> ListSteps;
	}

	public class ForkMigrationResult {
		public string DatabaseName;
		///<summary>Steps that ran, whether or not they changed anything.</summary>
		public List<ForkMigrationStep> ListStepsRun=new List<ForkMigrationStep>();
		///<summary>Steps that ran and changed the schema or data.</summary>
		public List<ForkMigrationStep> ListStepsChanged=new List<ForkMigrationStep>();
		///<summary>Steps that were not run because they had already finished.</summary>
		public List<ForkMigrationStep> ListStepsSkipped=new List<ForkMigrationStep>();
		///<summary>Every statement that changed the database, prefixed with the step level. The runner's own preference rows are not listed.</summary>
		public List<string> ListChanges=new List<string>();
	}
}
