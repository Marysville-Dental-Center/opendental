// Tables that only this program has: their declaration for the schema verifier and the adoption tools.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenPmsDentalBusiness.ForkMigrations {
	///<summary>The tables of one feature that are not Open Dental schema (fork-only tables), with their exact definitions. A feature package derives
	///one class from this in its own file, and its fork migration steps create the tables from these definitions. ForkTables finds every subclass by
	///reflection, as ForkMigrationRunner finds the steps, so adding one never edits a shared list. The schema verifier allows these tables as extra
	///tables (SchemaVerifier.DocumentedExceptions), ForkTables.VerifyDatabase checks their definitions, and the adoption tools expect them to be
	///added empty.</summary>
	public abstract class ForkTableSet {
		///<summary>The feature the tables belong to, for messages, for example "Routine maintenance".</summary>
		public abstract string Feature { get; }

		///<summary>The fork migration levels that create the tables, for messages, for example "2026110201 to 2026110205".</summary>
		public abstract string Levels { get; }

		///<summary>One CREATE TABLE statement per table, written exactly as SHOW CREATE TABLE prints it on MariaDB 10.11 (without AUTO_INCREMENT
		///values), so that a database the steps created matches it exactly. Table names are lower case and the primary key is a bigint named
		///&lt;Table&gt;Num, as in Open Dental's own tables.</summary>
		public abstract string[] ArrayCreateTables { get; }

		///<summary>The definition of one table of this set, parsed. Throws when the set has no such table.</summary>
		public string GetCreateTable(string tableName) {
			string statement=ArrayCreateTables.FirstOrDefault(x => string.Equals(SchemaTable.Parse(x).Name,tableName,StringComparison.OrdinalIgnoreCase));
			if(statement==null) {
				throw new ApplicationException(GetType().Name+" declares no table "+tableName+".");
			}
			return statement;
		}
	}

	///<summary>Every fork-only table this build declares (ForkTableSet subclasses in this assembly).</summary>
	public static class ForkTables {
		private static List<ForkTableSet> _listSets;
		private static SchemaSnapshot _snapshot;
		private static readonly object _lock=new object();

		///<summary>Every ForkTableSet of this assembly, ordered by class name. Throws when two sets declare the same table, or a table name is
		///also a table of the Open Dental 26.1.70 reference.</summary>
		public static List<ForkTableSet> GetSets() {
			lock(_lock) {
				if(_listSets==null) {
					List<ForkTableSet> listSets=typeof(ForkTableSet).Assembly.GetTypes()
						.Where(x => x.IsClass && !x.IsAbstract && typeof(ForkTableSet).IsAssignableFrom(x))
						.OrderBy(x => x.FullName,StringComparer.Ordinal)
						.Select(x => (ForkTableSet)Activator.CreateInstance(x))
						.ToList();
					SchemaSnapshot snapshot=new SchemaSnapshot();
					SchemaSnapshot snapshotReference=SchemaVerifier.GetReference();
					foreach(ForkTableSet set in listSets) {
						foreach(string statement in set.ArrayCreateTables) {
							SchemaTable table=SchemaTable.Parse(statement);
							if(snapshot.HasTable(table.Name)) {
								throw new ApplicationException("Fork table "+table.Name+" is declared twice.");
							}
							if(snapshotReference.HasTable(table.Name)) {
								throw new ApplicationException("Fork table "+table.Name+" ("+set.GetType().Name+") is an Open Dental table.");
							}
							snapshot.AddTable(table);
						}
					}
					_snapshot=snapshot;
					_listSets=listSets;
				}
				return _listSets;
			}
		}

		///<summary>Every fork-only table, parsed from its declaration.</summary>
		public static SchemaSnapshot GetSnapshot() {
			GetSets();
			return _snapshot;
		}

		///<summary>The names of every fork-only table, sorted.</summary>
		public static List<string> GetTableNames() {
			return GetSnapshot().Tables.Select(x => x.Name).OrderBy(x => x,StringComparer.Ordinal).ToList();
		}

		///<summary>True when the table is a fork-only table this build declares (case is ignored).</summary>
		public static bool IsForkTable(string tableName) {
			return !string.IsNullOrEmpty(tableName) && GetSnapshot().HasTable(tableName);
		}

		///<summary>The set that declares the table, or null.</summary>
		public static ForkTableSet GetSet(string tableName) {
			return GetSets().FirstOrDefault(x => x.ArrayCreateTables.Any(y => string.Equals(SchemaTable.Parse(y).Name,tableName,StringComparison.OrdinalIgnoreCase)));
		}

		///<summary>The schema verifier's exceptions for fork-only tables: each is allowed as an extra table, since the Open Dental reference does not
		///have it. Their definitions are checked by VerifyDatabase instead.</summary>
		public static List<SchemaException> GetSchemaExceptions() {
			List<SchemaException> listExceptions=new List<SchemaException>();
			foreach(ForkTableSet set in GetSets()) {
				foreach(string statement in set.ArrayCreateTables) {
					listExceptions.Add(new SchemaException(SchemaDifferenceKind.ExtraTable,SchemaTable.Parse(statement).Name,null,
						"Open PMS - Dental table ("+set.Feature+", fork migration "+set.Levels+"), not Open Dental schema. Checked by ForkTables.VerifyDatabase."));
				}
			}
			return listExceptions;
		}

		///<summary>Compares the fork-only tables of the database (default: the connected one) with their declarations: every declared table must exist
		///with exactly the declared columns, keys and options. Other tables are not looked at.</summary>
		public static SchemaVerifyResult VerifyDatabase(string databaseName=null) {
			SchemaSnapshot snapshotDatabase=SchemaSnapshot.FromDatabase(databaseName);
			SchemaSnapshot snapshotActual=new SchemaSnapshot();
			foreach(SchemaTable table in snapshotDatabase.Tables.Where(x => IsForkTable(x.Name))) {
				snapshotActual.AddTable(table);
			}
			return SchemaVerifier.Compare(GetSnapshot(),snapshotActual);
		}
	}
}
