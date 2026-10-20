// The seam between the version guard (M1-03) and the fork migration runner (M1-01).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using System.Reflection;

namespace OpenDentBusiness {
	///<summary>Implemented by the fork migration runner (package M1-01). The legacy ConvertDatabases chain is frozen at 24.3.63; every later schema
	///step runs in the runner, keyed by the ForkSchemaLevel marker. The guard needs only these members, so it never depends on how the runner finds
	///its steps.</summary>
	public interface IForkMigrationRunner {
		///<summary>The last fork migration step this build knows. ForkVersions.SCHEMA_LEVEL_BASELINE when it has none.</summary>
		string LatestLevel { get; }
		///<summary>Orders two schema levels: &lt;0, 0 or &gt;0. Must accept SCHEMA_LEVEL_BASELINE.</summary>
		int CompareLevels(string levelA,string levelB);
		///<summary>Runs every step above the database's ForkSchemaLevel, in order, and advances ForkSchemaLevel (and OdSchemaCompat when the schema
		///verifier passes) after each one. Every step is idempotent. Throws on failure.</summary>
		void RunPending(Action<string> actionLog);
	}

	///<summary>Finds the fork migration runner. The runner registers itself by implementing IForkMigrationRunner in OpenDentBusiness (any public
	///non-abstract class with a parameterless constructor); tests and tools can also set Runner. Until a runner exists the baseline runner is used:
	///no steps, latest level SCHEMA_LEVEL_BASELINE.</summary>
	public class ForkMigrationHandOff {
		private static IForkMigrationRunner _runner;
		private static readonly object _lock=new object();

		///<summary>The registered runner, discovered on first use.</summary>
		public static IForkMigrationRunner Runner {
			get {
				lock(_lock) {
					if(_runner==null) {
						_runner=Discover();
					}
					return _runner;
				}
			}
			set {
				lock(_lock) {
					_runner=value;
				}
			}
		}

		///<summary>True when the runner knows at least one step beyond the baseline. Until then databases on the 24.3.63 line keep working without
		///fork markers.</summary>
		public static bool HasSteps(IForkMigrationRunner runner) {
			return runner!=null && runner.CompareLevels(runner.LatestLevel,ForkVersions.SCHEMA_LEVEL_BASELINE)>0;
		}

		private static IForkMigrationRunner Discover() {
			Type[] arrayTypes;
			try {
				arrayTypes=typeof(IForkMigrationRunner).Assembly.GetTypes();
			}
			catch(ReflectionTypeLoadException ex) {
				arrayTypes=ex.Types.Where(x => x!=null).ToArray();//Types whose dependencies are missing cannot be the runner.
			}
			Type typeRunner=arrayTypes
				.Where(x => typeof(IForkMigrationRunner).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract && x.IsPublic
					&& x!=typeof(BaselineForkMigrationRunner) && x.GetConstructor(Type.EmptyTypes)!=null)
				.OrderBy(x => x.FullName)
				.FirstOrDefault();
			if(typeRunner==null) {
				return new BaselineForkMigrationRunner();
			}
			return (IForkMigrationRunner)Activator.CreateInstance(typeRunner);
		}
	}

	///<summary>The runner used until M1-01's runner is merged: no steps. Levels compare as numbers when both are numbers, otherwise as
	///case-insensitive text.</summary>
	public class BaselineForkMigrationRunner:IForkMigrationRunner {
		public string LatestLevel {
			get { return ForkVersions.SCHEMA_LEVEL_BASELINE; }
		}

		public int CompareLevels(string levelA,string levelB) {
			return CompareLevelsDefault(levelA,levelB);
		}

		public void RunPending(Action<string> actionLog) {
			//No fork migration steps exist yet.
		}

		///<summary>Numbers compare numerically; anything else compares as text. Null sorts lowest.</summary>
		public static int CompareLevelsDefault(string levelA,string levelB) {
			if(levelA==null || levelB==null) {
				return (levelA==null?0:1)-(levelB==null?0:1);
			}
			if(long.TryParse(levelA,out long numberA) && long.TryParse(levelB,out long numberB)) {
				return numberA.CompareTo(numberB);
			}
			return string.Compare(levelA,levelB,StringComparison.OrdinalIgnoreCase);
		}
	}
}
