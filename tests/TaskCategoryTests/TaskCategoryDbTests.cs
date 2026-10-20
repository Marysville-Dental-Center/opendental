// Integration tests for task categories against a scratch MariaDB database on this computer. The database gets the Open Dental 25.3.48 schema
// from the reference snapshot embedded in OpenDentBusiness (SchemaVerifier), so task.Category and task.TriagePosition are exactly as an office
// database has them, and is filled with synthetic rows only. The tests are skipped (inconclusive) when no server answers at 127.0.0.1:3306 as
// root with a blank password, or when TASKCATEGORY_TEST_DB=skip.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenDentBusiness;
using OpenDentBusiness.ForkMigrations;

namespace TaskCategoryTests {
	[TestClass]
	public class TaskCategoryDbTests {
		private const string SERVER="127.0.0.1";
		private const string CONNECTION="Server="+SERVER+";Port=3306;User ID=root;Password=;Connection Timeout=15";
		///<summary>Unique to this run, because several worktrees and test runners share the one local server. Always starts with taskcat_.</summary>
		private static readonly string _dbName="taskcat_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000);
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("TASKCATEGORY_TEST_DB")=="skip") {
				_skipReason="TASKCATEGORY_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
				return;
			}
			//The server answers, so from here on a failure is a real failure, not a reason to skip.
			using(MySqlConnection connection=new MySqlConnection(CONNECTION+";Database="+_dbName)) {
				connection.Open();
				foreach(SchemaTable table in SchemaVerifier.GetReference25_3_48().Tables) {
					Exec(connection,table.ToDdl());
				}
			}
			RemotingClient.MiddleTierRole=MiddleTierRole.ClientDirect;
			new DataConnection().SetDb(SERVER,_dbName,"root","","","",DatabaseType.MySql,true);
			Security.CurUser=new Userod { UserNum=1,UserName="Synthetic" };
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS `"+_dbName+"`");
				}
			}
			catch(Exception) {
				//Leaving the scratch database behind is harmless.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			foreach(string table in new[] { "task","taskhist","taskancestor","securitylog","securityloghash","preference" }) {
				DataCore.NonQ("DELETE FROM "+table);
			}
			//The preferences the task code reads, at Open Dental's defaults.
			foreach(KeyValuePair<string,string> pair in _dictPrefsDefault) {
				DataCore.NonQ("INSERT INTO preference (PrefName,ValueString) VALUES('"+pair.Key+"','"+pair.Value+"')");
			}
			Prefs.RefreshCache();
		}

		private static readonly Dictionary<string,string> _dictPrefsDefault=new Dictionary<string,string> {
			{ "RandomPrimaryKeys","0" },
			{ "TaskCategoryShow","0" },
		};

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		private static long GetLong(string command) {
			return Convert.ToInt64(DataCore.GetScalar(command));
		}

		///<summary>A task row the way Open Dental 25.3 writes one for an office that uses categories, with the HQ triage position filled in as
		///well. Returns its TaskNum.</summary>
		private static long InsertOfficeTaskRow(long category,int triagePosition) {
			DataCore.NonQ("INSERT INTO task (TaskListNum,DateTask,KeyNum,Descript,TaskStatus,IsRepeating,DateType,FromNum,ObjectType,DateTimeEntry,"
				+"UserNum,DateTimeFinished,PriorityDefNum,ReminderGroupId,ReminderType,ReminderFrequency,DateTimeOriginal,DescriptOverride,IsReadOnly,"
				+"Category,TriagePosition) VALUES (0,'0001-01-01',0,'Synthetic office task',0,0,0,0,0,NOW(),1,'0001-01-01 00:00:00',0,'',0,0,NOW(),'',0,"
				+category+","+triagePosition+")");
			return GetLong("SELECT MAX(TaskNum) FROM task");
		}

		private static void SetPref(string valueString) {
			DataCore.NonQ("UPDATE preference SET ValueString='"+valueString+"' WHERE PrefName='TaskCategoryShow'");
			Prefs.RefreshCache();
		}

		[TestMethod]
		public void RenamedColumn_ReadsAndWrites() {
			Task task=new Task { Descript="Synthetic task",DateTimeEntry=DateTime.Now,UserNum=1,Category=11 };
			long taskNum=Tasks.Insert(task);
			Assert.AreEqual(11,GetLong("SELECT Category FROM task WHERE TaskNum="+taskNum),"Insert writes task.Category.");
			Assert.AreEqual(11,Tasks.GetOne(taskNum).Category,"GetOne reads task.Category.");
			Assert.AreEqual(11,Tasks.GetMany(new List<long> { taskNum })[0].Category,"List reads go through the same CRUD.");
			task.Category=0;
			Tasks.Update(task);
			Assert.AreEqual(0,GetLong("SELECT Category FROM task WHERE TaskNum="+taskNum),"None saves as 0.");
		}

		[TestMethod]
		public void OfficeTask_KeepsCategoryAndTriagePosition_WhenEdited() {
			long taskNum=InsertOfficeTaskRow(category:12,triagePosition:4);
			Task taskOld=Tasks.GetOne(taskNum);
			Assert.AreEqual(12,taskOld.Category);
			Assert.AreEqual(4,taskOld.TriagePosition);
			Task task=taskOld.Copy();
			task.Descript="Synthetic office task, edited";
			Tasks.Update(task,taskOld);//The Task window's save path: checks the row was not changed by someone else, logs, then saves.
			DataTable table=DataCore.GetTable("SELECT Descript,Category,TriagePosition FROM task WHERE TaskNum="+taskNum);
			Assert.AreEqual("Synthetic office task, edited",table.Rows[0]["Descript"].ToString());
			Assert.AreEqual(12,Convert.ToInt64(table.Rows[0]["Category"]),"Editing a task never drops its category.");
			Assert.AreEqual(4,Convert.ToInt32(table.Rows[0]["TriagePosition"]),"The HQ triage position is kept as it is.");
		}

		[TestMethod]
		public void TaskHist_SavesTheCategory() {
			long taskNum=InsertOfficeTaskRow(category:13,triagePosition:0);
			TaskHists.Insert(new TaskHist(Tasks.GetOne(taskNum)));
			Assert.AreEqual(13,GetLong("SELECT Category FROM taskhist WHERE TaskNum="+taskNum));
		}

		[TestMethod]
		public void TaskCategoryShow_TurnsTheUiOnAndOff_AndOffKeepsTheData() {
			long taskNum=InsertOfficeTaskRow(category:12,triagePosition:0);
			SetPref("0");
			Assert.IsFalse(Tasks.IsCategoryShown(),"Off by default, as Open Dental ships it.");
			Assert.IsTrue(Prefs.UpdateBool(PrefName.TaskCategoryShow,true),"Tasks Preferences saves the checkbox.");
			Prefs.RefreshCache();
			Assert.IsTrue(Tasks.IsCategoryShown());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='TaskCategoryShow'"));
			Assert.IsTrue(Prefs.UpdateBool(PrefName.TaskCategoryShow,false));
			Prefs.RefreshCache();
			Assert.IsFalse(Tasks.IsCategoryShown());
			Assert.AreEqual(12,GetLong("SELECT Category FROM task WHERE TaskNum="+taskNum),"Turning the preference off hides the UI only.");
		}

		[TestMethod]
		public void TaskCategoryShow_OnInAnOfficeDatabase_IsRead() {
			//An office that turned the preference on in Open Dental 25.3 keeps it on after adoption.
			SetPref("1");
			Assert.IsTrue(Tasks.IsCategoryShown());
		}
	}
}
