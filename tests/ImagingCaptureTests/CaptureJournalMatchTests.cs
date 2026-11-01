// This file is part of the open source dental practice management software derived from Open Dental.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using ImagingCapture.Recovery;
using ImagingCapture.Save;
using ImagingDeviceManager.Capture;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace ImagingCapture.Tests {
	///<summary>CaptureJournalMatch (design 7.1): a journal copy has its document when an ImageCreate entry of the patient names its captureId for an
	///existing document, or a document of the patient has the DateCreated the saver would have given it, within a second.</summary>
	[TestClass]
	public class CaptureJournalMatchTests {
		private static readonly DateTime _receivedAt=new DateTime(2026,10,31,10,0,5,250);
		private RecoveryFiles _files;

		[TestInitialize]
		public void Setup() {
			_files=new RecoveryFiles();
		}

		[TestCleanup]
		public void Cleanup() {
			_files.Dispose();
		}

		[TestMethod]
		public void ExpectedDateCreated_AcquiredAtThenReceiveTime() {
			CaptureFileEntry entry=_files.Journal(RecoveryFiles.SessionFields(acquiredAt:"2026-10-31T09:59:58.900+00:00"),_receivedAt);
			Assert.AreEqual(new DateTimeOffset(2026,10,31,9,59,58,900,TimeSpan.Zero).LocalDateTime,CaptureJournalMatch.GetExpectedDateCreated(entry));
			Dictionary<string,string> dictFields=RecoveryFiles.SessionFields();
			dictFields["Metadata"]="";
			CaptureFileEntry entryNoMetadata=_files.Journal(dictFields,_receivedAt);
			Assert.AreEqual(_receivedAt,CaptureJournalMatch.GetExpectedDateCreated(entryNoMetadata));
		}

		[TestMethod]
		public void Match_ByCaptureId_InTheSaversCaptureEntry() {
			CaptureFileEntry entry=_files.Journal(RecoveryFiles.SessionFields(captureId:"cap-\"x\""),_receivedAt);
			List<Document> listDocuments=new List<Document> { new Document { DocNum=501,PatNum=42,DateCreated=new DateTime(2026,1,1) } };
			List<SecurityLog> listSecurityLogs=new List<SecurityLog> {
				new SecurityLog { FKey=501,PatNum=42,LogText="Capture: device \"Sensor\", captureId \"cap-'x'\", 8-bit, target Slot 3." },
			};
			string how;
			Assert.AreEqual(501,CaptureJournalMatch.FindDocument(entry,listDocuments,listSecurityLogs,out how)?.DocNum);
			Assert.AreEqual("captureId",how);
			Assert.AreEqual("\"cap-'x'\"",CaptureJournalMatch.QuoteLikeTheSaver("cap-\"x\""),"Quoted as RadiographSavePlanner.BuildCaptureLogText does.");
		}

		[TestMethod]
		public void Match_CaptureIdOfADeletedDocument_NoMatch() {
			CaptureFileEntry entry=_files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			List<SecurityLog> listSecurityLogs=new List<SecurityLog> { new SecurityLog { FKey=501,PatNum=42,LogText="Capture: captureId \"cap-7\"." } };
			List<Document> listDocuments=new List<Document> { new Document { DocNum=502,PatNum=42,DateCreated=new DateTime(2026,1,1) } };
			string how;
			Assert.IsNull(CaptureJournalMatch.FindDocument(entry,listDocuments,listSecurityLogs,out how),"DocNum 501 no longer exists.");
		}

		[TestMethod]
		public void Match_ByTime_WithinASecond_OfAcquiredAt() {
			CaptureFileEntry entry=_files.Journal(RecoveryFiles.SessionFields(captureId:"",acquiredAt:"2026-10-31T09:59:58.900+00:00"),_receivedAt);
			DateTime expected=CaptureJournalMatch.GetExpectedDateCreated(entry);
			DateTime stored=new DateTime(expected.Year,expected.Month,expected.Day,expected.Hour,expected.Minute,expected.Second);//whole seconds
			string how;
			Assert.AreEqual(601,CaptureJournalMatch.FindDocument(entry,new List<Document> { new Document { DocNum=601,PatNum=42,DateCreated=stored } },null,
				out how)?.DocNum);
			Assert.AreEqual("time",how);
			Assert.IsNull(CaptureJournalMatch.FindDocument(entry,new List<Document> { new Document { DocNum=602,PatNum=42,DateCreated=stored.AddSeconds(3) } },
				null,out how),"Another exposure, seconds later.");
			Assert.IsNull(CaptureJournalMatch.FindDocument(entry,new List<Document>(),null,out how));
		}

		[TestMethod]
		public void DocNumsToCheck_FromADayBeforeTheOldestCopy() {
			CaptureFileEntry entry=_files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
			List<Document> listDocuments=new List<Document> {
				new Document { DocNum=1,DateCreated=_receivedAt.AddDays(-3) },
				new Document { DocNum=2,DateCreated=_receivedAt.AddHours(-20) },
				new Document { DocNum=3,DateCreated=_receivedAt.AddMinutes(1) },
			};
			CollectionAssert.AreEqual(new long[] { 2,3 },CaptureJournalMatch.GetDocNumsToCheck(new[] { entry },listDocuments).ToArray());
			Assert.AreEqual(0,CaptureJournalMatch.GetDocNumsToCheck(new CaptureFileEntry[0],listDocuments).Count);
		}

		[TestMethod]
		public void RecoveredAssign_IsMatchedAfterwards_ByItsOwnLogEntry() {
			using(RecoveryHarness harness=new RecoveryHarness()) {
				CaptureFileEntry entry=harness.Files.Journal(RecoveryFiles.SessionFields(),_receivedAt);
				CaptureFilingResult result=harness.Filer.Assign(entry,0,42);
				Assert.IsTrue(result.IsDone,result.Error);
				SecurityLog securityLog=harness.Store.ListLogEntries.Single();
				string how;
				Assert.AreEqual(result.Document.DocNum,CaptureJournalMatch.FindDocument(entry,new List<Document> { result.Document },
					new List<SecurityLog> { securityLog },out how)?.DocNum);
				Assert.AreEqual("captureId",how);
				Assert.AreEqual(CaptureJournalMatch.GetExpectedDateCreated(entry),result.Document.DateCreated,"The saver's DateCreated rule, so the time matches too.");
			}
		}
	}
}
