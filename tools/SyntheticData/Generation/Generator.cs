// Licensed under the GNU General Public License, version 2. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SyntheticData.Infrastructure;

namespace SyntheticData.Generation {
	///<summary>Runs the whole generation into an empty schema copy: optional configuration copy, practice set-up, schedules, families,
	///practice-wide rows and optional filler rows.</summary>
	public static class Generator {
		public static World Run(Db db,string database,GenerationSettings settings,Action<string> log) {
			LocalOnly.EnsureWritableDatabaseName(database);
			db.Use(database);
			SchemaInfo schema=SchemaInfo.Load(db,database);
			if(!string.IsNullOrEmpty(settings.ConfigFrom)) {
				log("Copying configuration tables from "+settings.ConfigFrom);
				PracticeGenerator.CopyConfiguration(db,schema,database,settings.ConfigFrom);
			}
			DateTime timestampValue=settings.AsOf.Date.AddHours(18);
			World world=new World {
				Settings=settings,
				Sink=new DataSink(db,schema,timestampValue),
				AsOf=settings.AsOf.Date,
				TimestampValue=timestampValue,
			};
			log("Practice set-up");
			PracticeGenerator.Run(world,db);
			log("Schedules");
			ScheduleGenerator.Run(world);
			log("Families ("+settings.FamilyCount+")");
			int lastPercent=-1;
			FamilySimulator.RunAll(world,(done,total) => {
				int percent=done*100/total;
				if(percent/10!=lastPercent/10) {
					lastPercent=percent;
					log("  "+done+" of "+total+" families");
				}
			});
			log("Practice-wide rows");
			PracticeWideGenerator.Run(world);
			world.Sink.FlushAll();
			if(settings.FillRemaining>0) {
				log("Filler rows for tables that are still empty");
				FillRemaining(db,world,settings.FillRemaining);
				world.Sink.FlushAll();
			}
			return world;
		}

		///<summary>Tables the filler leaves alone: rows there would change how an Open Dental or fork client behaves on start-up (update files,
		///version history, registration, maintenance runs, replication).</summary>
		public static readonly HashSet<string> SetFillerExcludedTables=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
			"documentmisc","updatehistory","registrationkey","databasemaintenance","replicationserver","preference","activeinstance",
			"signalod","alertitem","alertread","alertsub","scheduledprocess","sessiontoken","loginattempt","centralconnection",
		};

		///<summary>Adds schema-valid rows to every table that is still empty: integers and money count up, text is "synthetic filler n",
		///dates are the as-of date. Foreign keys are not meaningful. Opt-in, for checksum coverage of all 422 tables.</summary>
		public static void FillRemaining(Db db,World world,int rowsPerTable) {
			Rng rng=Rng.Derive(world.Settings.Seed,"filler");
			SchemaInfo schema=world.Sink.Schema;
			foreach(TableInfo table in schema.DictTables.Values) {
				if(SetFillerExcludedTables.Contains(table.Name)) {
					continue;
				}
				if(db.ScalarLong("SELECT COUNT(*) FROM `"+table.Name+"`")>0) {
					continue;
				}
				for(int i=1;i<=rowsPerTable;i++) {
					Row row=new Row();
					foreach(ColumnInfo column in table.ListColumns.Where(x => !x.IsGenerated)) {
						row[column.Name]=FillerValue(world,table,column,i,rng);
					}
					world.Sink.Insert(table.Name,row);
				}
			}
		}

		private static object FillerValue(World world,TableInfo table,ColumnInfo column,int index,Rng rng) {
			if(column.IsPrimaryKey) {
				if(column.IsInteger) {
					return column.IsAutoIncrement ? world.Sink.NextId(table.Name) : (object)(long)index;
				}
				return "filler"+index;
			}
			if(column.IsInteger) {
				if(column.ColumnType.StartsWith("tinyint")) {
					return index%2;
				}
				return (long)index;
			}
			if(column.IsDecimal) {
				return (decimal)index*1.25m;
			}
			if(column.IsDateOnly || column.IsDateTime) {
				return world.AsOf.AddDays(-index);
			}
			if(column.IsTime) {
				return new TimeSpan(9,index%60,0);
			}
			if(column.IsBinary) {
				return rng.Bytes(8);
			}
			if(column.IsText) {
				string text="synthetic filler "+index;
				if(column.MaxLength.HasValue && text.Length>column.MaxLength.Value) {
					text=text.Substring(0,(int)column.MaxLength.Value);
				}
				return text;
			}
			return "";
		}

		public static string Summary(World world) {
			IReadOnlyDictionary<string,long> dictCounts=world.Sink.RowCounts;
			long total=dictCounts.Values.Sum();
			return dictCounts.Count+" tables, "+total.ToString("N0",CultureInfo.InvariantCulture)+" rows";
		}
	}
}
