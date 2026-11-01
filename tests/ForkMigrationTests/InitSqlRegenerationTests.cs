// db/init/new-database.sql must be exactly what its generator writes today: a new fork migration step (or a seed or scrub change) without a
// regenerated file fails here. The generator builds a whole database, so this test lives with the slow fork migration assemblies (run-tests
// -Mode Full, or -Include ForkMigrationTests) rather than in tests/InitSqlTests, which checks the committed file itself. Skipped (inconclusive)
// when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ForkMigrationTests {
	[TestClass]
	public class InitSqlRegenerationTests {
		private const string REPO_PATH_FILE=@"db\init\new-database.sql";
		///<summary>DevDbTool as the full build of OpenDental.Local.slnf leaves it.</summary>
		private const string REPO_PATH_TOOL=@"tools\DevDbTool\bin\Debug\DevDbTool.exe";

		[TestMethod]
		public void Generator_WritesExactlyTheCommittedFile() {
			string skipReason=TestDb.GetSkipReason();
			if(skipReason!=null) {
				Assert.Inconclusive(skipReason);
			}
			string pathCommitted=TestDb.FindRepoFile(REPO_PATH_FILE);
			string pathTool;
			try {
				pathTool=TestDb.FindRepoFile(REPO_PATH_TOOL);
			}
			catch(FileNotFoundException) {
				Assert.Fail(REPO_PATH_TOOL+" is not built. The full build of OpenDental.Local.slnf builds it.");
				return;
			}
			string database=TestDb.ScratchName("initsql");
			string pathOut=Path.Combine(Path.GetTempPath(),database+".sql");
			StringBuilder sbOutput=new StringBuilder();
			try {
				ProcessStartInfo processStartInfo=new ProcessStartInfo(pathTool,"--export-init-sql \""+pathOut+"\" --database "+database) {
					UseShellExecute=false,
					RedirectStandardOutput=true,
					RedirectStandardError=true,
					CreateNoWindow=true,
					WorkingDirectory=Path.GetDirectoryName(pathTool),
				};
				processStartInfo.EnvironmentVariables["DEVDB_PASSWORD"]=TestDb.Password;
				using(Process process=new Process { StartInfo=processStartInfo }) {
					process.OutputDataReceived+=(sender,e) => { if(e.Data!=null) { lock(sbOutput) { sbOutput.AppendLine(e.Data); } } };
					process.ErrorDataReceived+=(sender,e) => { if(e.Data!=null) { lock(sbOutput) { sbOutput.AppendLine(e.Data); } } };
					process.Start();
					process.BeginOutputReadLine();
					process.BeginErrorReadLine();
					if(!process.WaitForExit((int)TimeSpan.FromMinutes(15).TotalMilliseconds)) {
						process.Kill();
						Assert.Fail("The generator did not finish in 15 minutes.\r\n"+Tail(sbOutput));
					}
					process.WaitForExit();
					Assert.AreEqual(0,process.ExitCode,"DevDbTool --export-init-sql failed.\r\n"+Tail(sbOutput));
				}
				Assert.IsFalse(TestDb.DatabaseExists(database),"The generator drops its scratch database.");
				byte[] arrayExpected=File.ReadAllBytes(pathCommitted);
				byte[] arrayActual=File.ReadAllBytes(pathOut);
				string difference=FirstDifference(arrayExpected,arrayActual);
				Assert.IsNull(difference,REPO_PATH_FILE+" is not what the generator writes now. Run tools/New-InitSql.ps1 and commit the file.\r\n"
					+difference);
			}
			finally {
				if(TestDb.DatabaseExists(database)) {
					TestDb.Drop(database);
				}
				try {
					File.Delete(pathOut);
				}
				catch(Exception) {
					//A file in the temp folder; nothing depends on it.
				}
			}
		}

		///<summary>Null when the bytes are equal; otherwise the first line that differs, from both files.</summary>
		private static string FirstDifference(byte[] arrayExpected,byte[] arrayActual) {
			int length=Math.Min(arrayExpected.Length,arrayActual.Length);
			int index=0;
			while(index<length && arrayExpected[index]==arrayActual[index]) {
				index++;
			}
			if(index==length && arrayExpected.Length==arrayActual.Length) {
				return null;
			}
			return "Committed ("+arrayExpected.Length+" bytes): "+LineAt(arrayExpected,index)+"\r\nGenerated ("+arrayActual.Length+" bytes): "
				+LineAt(arrayActual,index);
		}

		private static string LineAt(byte[] arrayBytes,int index) {
			if(index>=arrayBytes.Length) {
				return "(ends before)";
			}
			int line=1;
			int start=0;
			for(int i=0;i<index;i++) {
				if(arrayBytes[i]==(byte)'\n') {
					line++;
					start=i+1;
				}
			}
			int end=start;
			while(end<arrayBytes.Length && arrayBytes[end]!=(byte)'\n' && end-start<300) {
				end++;
			}
			return "line "+line+": "+Encoding.UTF8.GetString(arrayBytes,start,end-start);
		}

		private static string Tail(StringBuilder sbOutput) {
			string text;
			lock(sbOutput) {
				text=sbOutput.ToString();
			}
			return text.Length>4000 ? text.Substring(text.Length-4000) : text;
		}
	}
}
