// V-PEARL2: the lines land in Logger's own dated files under the Pearl subfolder (26.1 spec, business rule 3). Uses a temporary folder.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness.Pearl;

namespace VPEARL2Tests {
	[TestClass]
	public class PearlLoggerFolderTests {
		[TestMethod]
		public void Lines_AreWrittenToLoggerPearl_WithTimestamps() {
			string dir=Path.Combine(Path.GetTempPath(),"vpearl2_logger_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			string loggerDirOld=Logger.LoggerDirOverride;
			bool isUnitTestOld=ODBuild.IsUnitTest;
			//Logger runs its once-a-day cleanup thread on the first line; mark it as done so no thread works in the temporary folder.
			FieldInfo fieldInfoCleanup=typeof(Logger).GetField("_lastLoggerCleanup",BindingFlags.Static|BindingFlags.NonPublic);
			object cleanupOld=fieldInfoCleanup?.GetValue(null);
			fieldInfoCleanup?.SetValue(null,DateTime.Now);
			Logger.CloseLogger();
			Logger.LoggerDirOverride=dir;
			ODBuild.IsUnitTest=false;//Logger writes nothing in the legacy unit test mode.
			try {
				using(LogCapture logCapture=new LogCapture("1")) {
					PearlLog.ActionWriteLine=PearlLog.WriteLineToLoggerFolder;//The real writer, into the temporary folder.
					using(PearlLog.BeginScope(42,"request-42")) {
						PearlLog.Error("synthetic error line");
						PearlLog.Info("synthetic info line");
						PearlLog.Verbose("synthetic verbose line");
					}
				}
				Logger.CloseLogger();
				Assert.IsTrue(Directory.Exists(Path.Combine(dir,"Pearl")),"Logger\\Pearl.");
				List<string> listLines=Logger.GetRecentLines(PearlLog.SUBDIRECTORY,50);
				Assert.AreEqual(2,listLines.Count,string.Join("\n",listLines));
				StringAssert.EndsWith(listLines[0],"\tERROR\tsynthetic error line\tdocument 42, request request-42");
				StringAssert.EndsWith(listLines[1],"\tINFO\tsynthetic info line\tdocument 42, request request-42");
				DateTime dateTimeStamp;
				Assert.IsTrue(DateTime.TryParseExact(listLines[0].Split('\t')[0],Logger.DATETIME_FORMAT,System.Globalization.CultureInfo.InvariantCulture,
					System.Globalization.DateTimeStyles.None,out dateTimeStamp),"Logger's time stamp: "+listLines[0]);
				string folderToday=Path.Combine(dir,"Pearl",DateTime.Today.ToString("yy-MM-dd"));
				Assert.IsTrue(Directory.GetFiles(folderToday,"*.txt").Length>0,"Logger's dated folder, so its age-based cleanup applies.");
			}
			finally {
				Logger.CloseLogger();
				Logger.LoggerDirOverride=loggerDirOld;
				ODBuild.IsUnitTest=isUnitTestOld;
				if(fieldInfoCleanup!=null && cleanupOld!=null) {
					fieldInfoCleanup.SetValue(null,cleanupOld);
				}
				try {
					Directory.Delete(dir,true);
				}
				catch(Exception) {
					//A leftover temporary folder is harmless.
				}
			}
		}
	}
}
