// Every SSH path goes through the host key check: only OpenPmsDentalStorage references SSH.NET, every SSH.NET client it creates is attached to
// the check and connects through it, every caller passes the program's store, and the window, its menu entry and the audit-only permission are
// wired (source checks; the window is WinForms and is never opened here).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace SshHostKeyTests {
	[TestClass]
	public class WiringTests {
		///<summary>The product's source files (not tests or tools), without bin and obj.</summary>
		private static List<string> GetProductSourceFiles(string pattern) {
			string root=TestDb.GetRepoRoot();
			List<string> listFiles=new List<string>();
			foreach(string folder in Directory.GetDirectories(root)) {
				string name=Path.GetFileName(folder);
				if(name.In("tests","tools","docs","db",".git",".claude","packages","Required dlls","installer","branding")) {
					continue;
				}
				foreach(string path in Directory.EnumerateFiles(folder,pattern,SearchOption.AllDirectories)) {
					string relative=path.Substring(root.Length);
					if(relative.Contains(Path.DirectorySeparatorChar+"bin"+Path.DirectorySeparatorChar)
						|| relative.Contains(Path.DirectorySeparatorChar+"obj"+Path.DirectorySeparatorChar))
					{
						continue;
					}
					listFiles.Add(path);
				}
			}
			return listFiles;
		}

		[TestMethod]
		public void OnlyTheStorageProject_ReferencesSshNet() {
			List<string> listProjects=GetProductSourceFiles("*.csproj").Where(x => File.ReadAllText(x).Contains("Renci.SshNet")).ToList();
			Assert.AreEqual(1,listProjects.Count,string.Join("\r\n",listProjects));
			StringAssert.EndsWith(listProjects[0],"OpenPmsDentalStorage.csproj");
		}

		[TestMethod]
		public void EveryClientTheStorageProjectCreates_IsAttached_AndConnectsThroughTheCheck() {
			string folder=Path.Combine(TestDb.GetRepoRoot(),"OpenPmsDentalStorage");
			int countClients=0;
			int countAttached=0;
			List<string> listProblems=new List<string>();
			foreach(string path in Directory.EnumerateFiles(folder,"*.cs",SearchOption.AllDirectories).Where(x => !x.Contains("\\obj\\") && !x.Contains("\\bin\\"))) {
				string source=File.ReadAllText(path);
				countClients+=Regex.Matches(source,@"new\s+(SftpClient|SshClient|ScpClient|NetConfClient)\s*\(").Count;
				countAttached+=Regex.Matches(source,@"SshHostKeyCheck\.Attach\(").Count;
				if(Path.GetFileName(path)!="SshHostKeyCheck.cs" && Regex.IsMatch(source,@"\.Connect\(\s*\)")) {
					listProblems.Add(path+" connects a client directly; use SshHostKeyCheck.Connect.");
				}
			}
			Assert.IsTrue(countClients>=2,"Sftp.Init and SftpSession create the clients.");
			Assert.AreEqual(countClients,countAttached,"Every client is attached to the host key check.");
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void EveryCaller_PassesTheProgramsStore() {
			List<string> listProblems=new List<string>();
			int countCalls=0;
			Regex regexCall=new Regex(@"(new\s+(OpenPmsDentalStorage\.)?Sftp\.\w+\(|new\s+SftpSession\(|Sftp\.IsConnectionValid\(|Sftp\.FileExists\()");
			foreach(string path in GetProductSourceFiles("*.cs").Where(x => !x.Contains(Path.DirectorySeparatorChar+"OpenPmsDentalStorage"+Path.DirectorySeparatorChar))) {
				string source=File.ReadAllText(path);
				foreach(Match match in regexCall.Matches(source)) {
					countCalls++;
					int end=source.IndexOf(';',match.Index);
					string statement=source.Substring(match.Index,end-match.Index);
					//An object initializer ends at its own semicolon; the store comes before the opening brace in any case.
					int brace=statement.IndexOf('{');
					if(brace>=0) {
						statement=statement.Substring(0,brace);
					}
					if(!statement.Contains("SshHostKeys.Store")) {
						listProblems.Add(Path.GetFileName(path)+": "+statement.Trim());
					}
				}
			}
			//CloudStorage (8), TsiTransLogs (2), FormArManager (3), ControlManage, FormPath, FormOpenDental, DentiCal, MercuryDE and ServiceHL7.
			Assert.IsTrue(countCalls>=19,"Calls found: "+countCalls);
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void Window_MenuAndButton_NeedSecurityAdmin() {
			string menus=TestDb.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDentalMenus.cs");
			StringAssert.Contains(menus,"menuItemSetup.Add(\"SSH Host Keys\",menuItemSshHostKeys_Click);");
			Assert.IsTrue(menus.IndexOf("\"Spell Check\"")<menus.IndexOf("\"SSH Host Keys\"") && menus.IndexOf("\"SSH Host Keys\"")<menus.IndexOf("\"Tasks\""),
				"In the Setup list, in its place.");
			string formOpenDental=TestDb.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			AssertHandlerChecksSecurityAdmin(formOpenDental,"private void menuItemSshHostKeys_Click(");
			string formPath=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormPath.cs");
			AssertHandlerChecksSecurityAdmin(formPath,"private void butSshHostKeys_Click(");
			StringAssert.Contains(TestDb.ReadRepoFile("OpenPmsDental","Forms","FormPath.designer.cs"),"this.tabSftp.Controls.Add(this.butSshHostKeys);");
			string csproj=TestDb.ReadRepoFile("OpenPmsDental","OpenPmsDental.csproj");
			StringAssert.Contains(csproj,"<Compile Include=\"Forms\\FormSshHostKeys.cs\">");
			StringAssert.Contains(csproj,"<Compile Include=\"Forms\\FormSshHostKeys.Designer.cs\">");
			string form=TestDb.ReadRepoFile("OpenPmsDental","Forms","FormSshHostKeys.cs");
			StringAssert.Contains(form,"SshHostKeys.AcceptRefusedKey(sshHostKey.SshHostKeyNum,sshHostKey.RefusedKeyType,sshHostKey.RefusedFingerprintSha256)",
				"Accept passes the refused key the window showed.");
			StringAssert.Contains(form,"SshHostKeys.Delete(sshHostKey.SshHostKeyNum)");
		}

		private static void AssertHandlerChecksSecurityAdmin(string source,string signature) {
			int start=source.IndexOf(signature);
			Assert.IsTrue(start>=0,signature+" not found.");
			string body=source.Substring(start,Math.Min(600,source.Length-start));
			int indexCheck=body.IndexOf("Security.IsAuthorized(EnumPermType.SecurityAdmin)");
			int indexOpen=body.IndexOf("new FormSshHostKeys()");
			Assert.IsTrue(indexCheck>0 && indexOpen>indexCheck,signature+" must check Security Admin before it opens the window.");
		}

		[TestMethod]
		public void Permission_IsAuditOnly_InTheFeaturesRange() {
			Assert.AreEqual(10100,(int)EnumPermType.SshHostKeys);
			Assert.AreEqual("SSH Host Keys",EnumPermType.SshHostKeys.GetDescription());
			Assert.IsTrue(ForkEnums.IsForkOnly(EnumPermType.SshHostKeys));
			Assert.AreEqual(ForkEnums.FEATURE_SSH_HOST_KEYS,ForkEnums.GetFeature(EnumPermType.SshHostKeys));
			Assert.IsTrue(ForkEnums.ListRanges.Any(x => !x.IsTinyInt && x.First==10100 && x.Last==10149 && x.Feature==ForkEnums.FEATURE_SSH_HOST_KEYS));
			Assert.IsFalse(GroupPermissions.IsReserved(EnumPermType.SshHostKeys));
			Assert.IsTrue(GroupPermissions.HasAuditTrail(EnumPermType.SshHostKeys),"The Audit Trail can filter on it.");
			Assert.IsFalse(GroupPermissions.PermTakesDates(EnumPermType.SshHostKeys));
			Assert.IsTrue((int)EnumPermType.SshHostKeys<=short.MaxValue,"securitylog.PermType is smallint(6).");
			string tree=TestDb.ReadRepoFile("OpenPmsDental","User Controls","UserControlSecurityTree.cs");
			Assert.IsFalse(tree.Contains("SetNode(EnumPermType.SshHostKeys)"),"Never granted, so not in the security tree.");
			string forkEnums=TestDb.ReadRepoFile("docs","port-notes","FORK-ENUMS.md");
			StringAssert.Contains(forkEnums,"| 10100-10149 | SSH host keys ");
			StringAssert.Contains(forkEnums,"| EnumPermType     | 10100 | SshHostKeys           | SSH host keys       |");
		}
	}
}
