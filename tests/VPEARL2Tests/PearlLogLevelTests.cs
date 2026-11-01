// V-PEARL2: the "Log Level" values and what each level writes.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlLogLevelTests {
		[TestMethod]
		public void ParseLogLevel_DefaultsToErrorsOnly() {
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel(null),"Missing row.");
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel(""));
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("   "));
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("0"));
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("-1"));
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("verbose"),"Not a number.");
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("1.5"));
			Assert.AreEqual(LogLevel.Error,PearlLog.ParseLogLevel("99999999999999999999"),"Out of range.");
		}

		[TestMethod]
		public void ParseLogLevel_OneAndTwo() {
			Assert.AreEqual(LogLevel.Information,PearlLog.ParseLogLevel("1"));
			Assert.AreEqual(LogLevel.Verbose,PearlLog.ParseLogLevel("2"));
			Assert.AreEqual(LogLevel.Verbose,PearlLog.ParseLogLevel(" 2 "),"Spaces around the value are ignored.");
			Assert.AreEqual(LogLevel.Verbose,PearlLog.ParseLogLevel("3"),"Above 2 reads as verbose (inferred).");
		}

		[TestMethod]
		public void LevelNumbers_FollowCodeBaseLogLevel() {
			//The property stores the number of CodeBase.LogLevel, as the 26.1 spec says.
			Assert.AreEqual(0,(int)LogLevel.Error);
			Assert.AreEqual(1,(int)LogLevel.Information);
			Assert.AreEqual(2,(int)LogLevel.Verbose);
			CollectionAssert.AreEqual(new[] { "0","1","2" },PearlLog.GetLogLevelValues());
			Assert.AreEqual(PearlLog.GetLogLevelValues().Count,PearlLog.GetLogLevelDisplays().Count);
			Assert.IsTrue(PearlLog.GetLogLevelDisplays()[0].Contains("default"));
			Assert.AreEqual("Log Level",PearlLog.LOG_LEVEL_PROPERTY,"The name on the public Pearl bridge page.");
			Assert.AreEqual("0",PearlLog.LOG_LEVEL_DEFAULT);
			Assert.AreEqual("Pearl",PearlLog.SUBDIRECTORY,"Logger\\Pearl, as the public Loggers page shows.");
		}

		[TestMethod]
		public void Level0_WritesErrorsOnly() {
			using(LogCapture logCapture=new LogCapture("0")) {
				PearlLog.Error("an error");
				PearlLog.Info("an upload");
				PearlLog.Verbose("an API call");
				Assert.AreEqual(1,logCapture.Lines.Count,logCapture.AllText);
				Assert.AreEqual("ERROR\tan error",logCapture.Lines[0]);
			}
		}

		[TestMethod]
		public void Level1_WritesErrorsAndInformation() {
			using(LogCapture logCapture=new LogCapture("1")) {
				PearlLog.Error("an error");
				PearlLog.Info("an upload");
				PearlLog.Verbose("an API call");
				CollectionAssert.AreEqual(new[] { "ERROR\tan error","INFO\tan upload" },logCapture.Lines);
			}
		}

		[TestMethod]
		public void Level2_WritesEverything() {
			using(LogCapture logCapture=new LogCapture("2")) {
				PearlLog.Error("an error");
				PearlLog.Info("an upload");
				PearlLog.Verbose("an API call");
				CollectionAssert.AreEqual(new[] { "ERROR\tan error","INFO\tan upload","VERBOSE\tan API call" },logCapture.Lines);
			}
		}

		[TestMethod]
		public void LevelChange_AppliesToTheNextLine() {
			using(LogCapture logCapture=new LogCapture("0")) {
				PearlLog.Verbose("hidden");
				logCapture.LogLevel="2";
				PearlLog.Verbose("shown");
				logCapture.LogLevel="0";
				PearlLog.Verbose("hidden again");
				CollectionAssert.AreEqual(new[] { "VERBOSE\tshown" },logCapture.Lines);
				Assert.IsTrue(PearlLog.IsEnabled(LogLevel.Error));
				Assert.IsFalse(PearlLog.IsEnabled(LogLevel.Information));
			}
		}

		[TestMethod]
		public void UnreadableProperty_IsErrorsOnly_AndLoggingNeverThrows() {
			using(LogCapture logCapture=new LogCapture("0")) {
				PearlLog.FuncGetPropVal=desc => throw new ApplicationException("Property not found: "+desc);
				Assert.AreEqual(LogLevel.Error,PearlLog.GetLogLevel());
				PearlLog.Error("still written");
				Assert.AreEqual(1,logCapture.Lines.Count,"The client secret can't be read either, and the line is still written.");
				PearlLog.ActionWriteLine=line => throw new System.IO.IOException("disk full");
				PearlLog.Error("not written, and no exception");
			}
		}

		[TestMethod]
		public void Scope_TagsLinesWithDocumentAndRequest_AndIsRestored() {
			using(LogCapture logCapture=new LogCapture("2")) {
				using(PearlLog.BeginScope(42,null)) {
					PearlLog.Verbose("one");
					using(PearlLog.BeginScope(42,"6f1c0b5e-1111-4d2a-9c3b-000000000001")) {
						PearlLog.Verbose("two");
					}
					PearlLog.Verbose("three");
				}
				PearlLog.Verbose("four");
				Assert.IsNull(PearlLog.ContextCur);
				CollectionAssert.AreEqual(new[] {
						"VERBOSE\tone\tdocument 42",
						"VERBOSE\ttwo\tdocument 42, request 6f1c0b5e-1111-4d2a-9c3b-000000000001",
						"VERBOSE\tthree\tdocument 42",
						"VERBOSE\tfour",
					},logCapture.Lines);
			}
		}

		[TestMethod]
		public void WorkerFailure_IsAnErrorAtEveryLevel() {
			using(LogCapture logCapture=new LogCapture("0")) {
				PearlLog.LogWorkerFailure(7,"req-7",new Exception("Retrieving AI results for an image timed out."));
				PearlLog.LogWorkerFailure(8,null,new Exception("Image was already sent to Pearl"));
				Assert.AreEqual(2,logCapture.Lines.Count);
				Assert.AreEqual("ERROR\tStopped: Retrieving AI results for an image timed out.\tdocument 7, request req-7",logCapture.Lines[0]);
				Assert.AreEqual("ERROR\tStopped: Image was already sent to Pearl\tdocument 8",logCapture.Lines[1]);
				Assert.IsNull(PearlLog.ContextCur,"The worker's scope ends with the line.");
			}
		}

		[TestMethod]
		public void DescribeException_NamesTypeAndInnerException() {
			Assert.AreEqual("unknown error",PearlLog.DescribeException(null));
			Assert.AreEqual("plain",PearlLog.DescribeException(new Exception("plain")));
			string text=PearlLog.DescribeException(new ApplicationException("outer",new TimeoutException("inner")));
			Assert.AreEqual("ApplicationException: outer (TimeoutException: inner)",text);
		}

		[TestMethod]
		public void EnsureLogLevelProperty_IgnoresOtherPrograms() {
			Assert.IsFalse(PearlLog.EnsureLogLevelProperty(null));
			OpenPmsDentalBusiness.Program program=new OpenPmsDentalBusiness.Program();
			program.ProgramNum=5;
			program.ProgName="Overjet";
			Assert.IsFalse(PearlLog.EnsureLogLevelProperty(program),"No database call for a program other than Pearl.");
			program.ProgName="Pearl";
			program.ProgramNum=0;
			Assert.IsFalse(PearlLog.EnsureLogLevelProperty(program),"A program link that was never saved.");
		}
	}
}
