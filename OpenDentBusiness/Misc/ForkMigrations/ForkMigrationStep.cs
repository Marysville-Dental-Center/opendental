using System;

namespace OpenDentBusiness.ForkMigrations {
	///<summary>Marks a class as a fork migration step and gives its level and name. The runner (ForkMigrationRunner) finds every non-abstract
	///subclass of ForkMigrationStep in this assembly and runs them in level order. A step without this attribute is an error, so a step can never be
	///skipped by accident.
	///Levels are yyyyMMddNN numbers: the date the step was written plus a two-digit sequence number, for example 2026101901. They must be unique.
	///Write a new step with today's date so it sorts after every step already merged.</summary>
	[AttributeUsage(AttributeTargets.Class,AllowMultiple=false,Inherited=false)]
	public class ForkMigrationAttribute:Attribute {
		///<summary>yyyyMMddNN. Steps run in ascending order.</summary>
		public long Level { get; }
		///<summary>Short description shown in the progress window and the log.</summary>
		public string Name { get; }

		public ForkMigrationAttribute(long level,string name) {
			Level=level;
			Name=name;
		}
	}

	///<summary>One fork migration step: schema changes and the data migrations that belong to them. Put each step in its own class, usually in its own
	///file under OpenDentBusiness/Misc/ForkMigrations/, and mark it with ForkMigrationAttribute. Feature packages add steps the same way; they never
	///edit a shared file to register one.
	///Rules for a step:
	///1. Idempotent. Apply must leave a database that is already in the target state unchanged, so the same steps upgrade a 24.3 database and leave
	///an Open Dental 25.3.48 database alone. Use the ForkMigrationContext helpers, which check before they change anything.
	///2. Data migrations and back-fills run only when the step itself just created the column (or other marker) they belong to, never on a database
	///that Open Dental already converted.
	///3. No transactions: the tables are MyISAM. The runner records a step as in progress before it starts and refuses to run again after a step was
	///interrupted, because the database then needs a restore.</summary>
	public abstract class ForkMigrationStep {
		///<summary>Runs the step. Throw to stop the migration.</summary>
		public abstract void Apply(ForkMigrationContext context);

		///<summary>The step's attribute. Throws if the class has none.</summary>
		public ForkMigrationAttribute Attribute {
			get {
				ForkMigrationAttribute attribute=(ForkMigrationAttribute)System.Attribute.GetCustomAttribute(GetType(),typeof(ForkMigrationAttribute));
				if(attribute==null) {
					throw new ApplicationException("Fork migration step "+GetType().FullName+" has no [ForkMigration] attribute.");
				}
				return attribute;
			}
		}

		public long Level {
			get {
				return Attribute.Level;
			}
		}

		public string Name {
			get {
				return Attribute.Name;
			}
		}

		public override string ToString() {
			return Level+" "+Name;
		}
	}
}
