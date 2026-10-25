// QueryFilters reads and Sync on a scratch database on the local MariaDB (backlog package A-QM). Synthetic data only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;

namespace QueryMonitorTests {
	///<summary>Builds one scratch database holding only the tables these tests touch: queryfilter with the exact Open Dental 25.3.48 definition (the
	///same DDL the fork migration step creates) and preference, which the generated CRUD reads for RandomPrimaryKeys. Each test empties
	///queryfilter first. The database is dropped at the end of the class.</summary>
	[TestClass]
	public class QueryFiltersDbTests {
		private const string Server="127.0.0.1";
		private const string User="root";
		private const string PREFIX="qmtest_";
		private static string _dbName;
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInit(TestContext testContext) {
			_skipReason=GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			_dbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
			using(MySqlConnection connection=OpenServer()) {
				Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				Exec(connection,"USE `"+_dbName+"`");
				Exec(connection,@"CREATE TABLE `queryfilter` (
  `QueryFilterNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `GroupName` varchar(255) NOT NULL,
  `FilterText` varchar(255) NOT NULL,
  PRIMARY KEY (`QueryFilterNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
				Exec(connection,@"CREATE TABLE `preference` (
  `PrefName` varchar(255) NOT NULL DEFAULT '',
  `ValueString` mediumtext NOT NULL,
  `PrefNum` bigint(20) NOT NULL AUTO_INCREMENT,
  `Comments` text DEFAULT NULL,
  PRIMARY KEY (`PrefNum`)
) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci");
				Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES ('RandomPrimaryKeys','0')");
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(Server,_dbName,User,"","","",DatabaseType.MySql,true);
			Cache.Refresh(InvalidType.Prefs);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_dbName==null || !_dbName.StartsWith(PREFIX) || Environment.GetEnvironmentVariable("QUERYMONITOR_KEEP_DB")=="1") {
				return;
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
			}
		}

		[TestInitialize]
		public void Init() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			DataCore.NonQ("DELETE FROM queryfilter");
		}

		private static string GetSkipReason() {
			if(Environment.GetEnvironmentVariable("QUERYMONITOR_TEST_DB")=="skip") {
				return "QUERYMONITOR_TEST_DB=skip";
			}
			try {
				using(MySqlConnection connection=OpenServer()) {
					return null;
				}
			}
			catch(Exception ex) {
				return "No local MariaDB at "+Server+":3306 as "+User+" with a blank password: "+ex.GetBaseException().Message;
			}
		}

		private static MySqlConnection OpenServer() {
			MySqlConnection connection=new MySqlConnection("Server="+Server+";Port=3306;User ID="+User+";Password=;Connection Timeout=15");
			connection.Open();
			return connection;
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static QueryFilter Make(string groupName,string filterText) {
			QueryFilter queryFilter=new QueryFilter();
			queryFilter.GroupName=groupName;
			queryFilter.FilterText=filterText;
			return queryFilter;
		}

		[TestMethod]
		public void InsertAndGet_RoundTripIncludingQuotes() {
			QueryFilters.Insert(Make("Signals","WHERE Note='it''s'"));
			QueryFilters.Insert(Make("Billing","FROM statement"));
			List<QueryFilter> listAll=QueryFilters.GetAll();
			Assert.AreEqual(2,listAll.Count);
			Assert.AreEqual("Billing",listAll[0].GroupName,"Ordered by group name.");
			List<QueryFilter> listSignals=QueryFilters.GetForGroup("Signals");
			Assert.AreEqual(1,listSignals.Count);
			Assert.AreEqual("WHERE Note='it''s'",listSignals[0].FilterText);
		}

		[TestMethod]
		public void Sync_InsertsUpdatesAndDeletes() {
			QueryFilter queryFilterKeep=Make("Signals","signalod");
			QueryFilter queryFilterEdit=Make("Signals","processid");
			QueryFilter queryFilterDelete=Make("Signals","computer");
			QueryFilters.Insert(queryFilterKeep);
			QueryFilters.Insert(queryFilterEdit);
			QueryFilters.Insert(queryFilterDelete);
			List<QueryFilter> listOld=QueryFilters.GetAll();
			List<QueryFilter> listNew=listOld.Select(x => x.Copy()).ToList();
			listNew.Find(x => x.QueryFilterNum==queryFilterEdit.QueryFilterNum).FilterText="ProcessId=";
			listNew.RemoveAll(x => x.QueryFilterNum==queryFilterDelete.QueryFilterNum);
			listNew.Add(Make("Signals","heartbeat"));
			Assert.IsTrue(QueryFilters.Sync(listNew,listOld));
			List<string> listTexts=QueryFilters.GetForGroup("Signals").Select(x => x.FilterText).OrderBy(x => x).ToList();
			CollectionAssert.AreEqual(new[] { "heartbeat","ProcessId=","signalod" },listTexts);
			Assert.IsNotNull(QueryFilters.GetAll().Find(x => x.QueryFilterNum==queryFilterKeep.QueryFilterNum),"Unchanged rows keep their key.");
			Assert.IsFalse(QueryFilters.Sync(QueryFilters.GetAll(),QueryFilters.GetAll()),"Nothing to write the second time.");
		}

		///<summary>Renaming a group changes GroupName on all of its rows, and deleting every row removes the group.</summary>
		[TestMethod]
		public void Sync_RenameAndRemoveWholeGroups() {
			QueryFilters.Insert(Make("Signals","signalod"));
			QueryFilters.Insert(Make("Signals","processid"));
			QueryFilters.Insert(Make("Billing","statement"));
			List<QueryFilter> listOld=QueryFilters.GetAll();
			List<QueryFilter> listNew=listOld.Select(x => x.Copy()).ToList();
			QueryFilters.RenameGroup(listNew,"Signals","Polling");
			listNew.RemoveAll(x => x.GroupName=="Billing");
			QueryFilters.Sync(QueryFilters.NormalizeForSave(listNew),listOld);
			List<QueryFilter> listAll=QueryFilters.GetAll();
			CollectionAssert.AreEqual(new[] { "Polling" },QueryFilters.GetGroupNames(listAll));
			Assert.AreEqual(2,listAll.Count);
		}

		///<summary>Another user's rows added after this window loaded are not deleted by Sync.</summary>
		[TestMethod]
		public void Sync_KeepsRowsAddedByOthersMeanwhile() {
			QueryFilters.Insert(Make("Signals","signalod"));
			List<QueryFilter> listOld=QueryFilters.GetAll();
			QueryFilters.Insert(Make("Theirs","patient"));//another workstation
			List<QueryFilter> listNew=listOld.Select(x => x.Copy()).ToList();
			listNew.Add(Make("Signals","computer"));
			QueryFilters.Sync(listNew,listOld);
			Assert.AreEqual(1,QueryFilters.GetForGroup("Theirs").Count);
			Assert.AreEqual(2,QueryFilters.GetForGroup("Signals").Count);
		}

		///<summary>The full path the Query Monitor takes: the saved rows of the active group become the monitor's filters.</summary>
		[TestMethod]
		public void SavedGroup_FiltersTheMonitor() {
			QueryFilters.Sync(QueryFilters.NormalizeForSave(new List<QueryFilter> {
				Make("Signals","FROM signalod"),Make("Signals","from SIGNALOD"),Make("Other","patient")
			}),new List<QueryFilter>());
			Assert.AreEqual(1,QueryFilters.GetForGroup("Signals").Count,"The duplicate is not saved.");
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.SetFilterTexts(QueryFilters.GetFilterTextsForGroup(QueryFilters.GetForGroup("Signals"),"Signals"));
			Assert.IsTrue(queryMonitor.IsFiltered("SELECT * FROM signalod WHERE SigDateTime > NOW()"));
			Assert.IsFalse(queryMonitor.IsFiltered("SELECT * FROM patient"));
		}
	}
}
