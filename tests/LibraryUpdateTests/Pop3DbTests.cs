// POP3 receive end to end (EmailMessages.ReceiveFromInbox) against the fake POP3 server and a scratch database made from db/init/new-database.sql:
// the new messages land in emailmessage as received email with their subject, sender and raw text, their UIDs in emailmessageuid, and a second
// run downloads nothing. Inconclusive without the local MariaDB. Synthetic data only. Licensed under the GNU General Public License, version 2 or
// later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;

namespace LibraryUpdateTests {
	[TestClass]
	public class Pop3DbTests {
		private static readonly string DB_NAME=ScratchDb.Name("pop3");
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			ScratchDb.DropStale();
			ScratchDb.Recreate(DB_NAME);
			ScratchDb.LoadNewDatabase(DB_NAME);
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason==null) {
				ScratchDb.DropQuietly(DB_NAME);
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Prefs.RefreshCache();
		}

		private static DataTable Query(string command) {
			using(MySqlConnection connection=ScratchDb.OpenServer(DB_NAME))
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection))
			using(MySqlDataReader reader=mySqlCommand.ExecuteReader())
			{
				DataTable table=new DataTable();
				table.Load(reader);
				return table;
			}
		}

		[TestMethod]
		public void ReceiveFromInbox_StoresNewMail_Once() {
			using(FakePop3Server fakePop3Server=new FakePop3Server(Pop3Tests.USER,Pop3Tests.PASSWORD,isTls:false)) {
				fakePop3Server.AddMessage("db-uid-1",Pop3Tests.Message("Pat Smith <pat@example.test>",Pop3Tests.USER,"Question about my visit",
					"Can I come at 10 instead?\r\n"));
				fakePop3Server.AddMessage("db-uid-2",Pop3Tests.Message("lab@example.test",Pop3Tests.USER,"=?utf-8?Q?R=C3=A9sultats?=",
					"Résultats prêts\r\n","text/plain; charset=utf-8",new UTF8Encoding(false)));
				EmailAddress emailAddress=Pop3Tests.MakeAddress(fakePop3Server.Port,false);
				Assert.AreEqual(2,EmailMessages.ReceiveFromInbox(emailAddress));
				Assert.IsNull(fakePop3Server.ServerError,fakePop3Server.ServerError?.ToString());
				DataTable tableMessages=Query("SELECT Subject,FromAddress,RecipientAddress,SentOrReceived,RawEmailIn,BodyText FROM emailmessage ORDER BY EmailMessageNum");
				Assert.AreEqual(2,tableMessages.Rows.Count);
				Assert.AreEqual("Question about my visit",tableMessages.Rows[0]["Subject"].ToString());
				StringAssert.Contains(tableMessages.Rows[0]["FromAddress"].ToString(),"pat@example.test");
				Assert.AreEqual(Pop3Tests.USER,tableMessages.Rows[0]["RecipientAddress"].ToString());
				Assert.AreEqual((int)EmailSentOrReceived.Received,Convert.ToInt32(tableMessages.Rows[0]["SentOrReceived"]));
				StringAssert.Contains(tableMessages.Rows[0]["RawEmailIn"].ToString(),"Can I come at 10 instead?");
				Assert.AreEqual("Résultats",tableMessages.Rows[1]["Subject"].ToString());
				StringAssert.Contains(tableMessages.Rows[1]["BodyText"].ToString(),"Résultats prêts");
				DataTable tableUids=Query("SELECT MsgId,RecipientAddress FROM emailmessageuid ORDER BY MsgId");
				CollectionAssert.AreEqual(new[] { "db-uid-1","db-uid-2" },tableUids.Rows.Cast<DataRow>().Select(x => x["MsgId"].ToString()).ToArray());
				Assert.AreEqual(Pop3Tests.USER,tableUids.Rows[0]["RecipientAddress"].ToString());
				//The next check of the inbox finds nothing new.
				Assert.AreEqual(0,EmailMessages.ReceiveFromInbox(emailAddress));
				Assert.AreEqual(2,Query("SELECT EmailMessageNum FROM emailmessage").Rows.Count);
				Assert.AreEqual(1,fakePop3Server.GetCommands().Count(x => x=="RETR 1"),"Downloaded once.");
			}
		}
	}
}
