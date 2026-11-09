// O-12 (docs/port-notes/DB-TLS.md): tools/Enable-LocalDbTls.ps1 without changing anything. Its certificate set, made in a temporary folder
// (-TestCertificatesFolder), checked offline; its my.ini edit on synthetic text; its refusal without elevation; its dry run. Nothing here
// changes the MariaDB service, its option file or a certificate store.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DbConnectionSecurityTests {
	[TestClass]
	public class DevDbTlsScriptTests {
		private string _folder;

		[TestInitialize]
		public void SetUp() {
			_folder=TestHelpers.NewTempFolder();
		}

		[TestCleanup]
		public void CleanUp() {
			TestHelpers.DeleteFolder(_folder);
		}

		private static string GetScriptPath() {
			string path=Path.Combine(GetRepoRoot(),"tools","Enable-LocalDbTls.ps1");
			Assert.IsTrue(File.Exists(path),path);
			return path;
		}

		private static string GetRepoRoot() {
			DirectoryInfo directoryInfo=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
			while(directoryInfo!=null
				&& !(File.Exists(Path.Combine(directoryInfo.FullName,"LICENSE")) && File.Exists(Path.Combine(directoryInfo.FullName,"NOTICE"))))
			{
				directoryInfo=directoryInfo.Parent;
			}
			Assert.IsNotNull(directoryInfo,"Repository root not found.");
			return directoryInfo.FullName;
		}

		///<summary>Runs a PowerShell script file without a profile, non-interactively (a question gets no answer, so nothing is ever confirmed).
		///Returns the exit code; output is the standard output and error.</summary>
		private static int RunPowerShell(string scriptPath,string arguments,out string output) {
			ProcessStartInfo processStartInfo=new ProcessStartInfo("powershell.exe",
				"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \""+scriptPath+"\" "+arguments) {
				UseShellExecute=false,
				CreateNoWindow=true,
				RedirectStandardOutput=true,
				RedirectStandardError=true,
				RedirectStandardInput=true,
			};
			using(Process process=Process.Start(processStartInfo)) {
				process.StandardInput.Close();
				System.Threading.Tasks.Task<string> taskError=process.StandardError.ReadToEndAsync();
				string standardOutput=process.StandardOutput.ReadToEnd();
				Assert.IsTrue(process.WaitForExit(300000),"The script did not finish within 5 minutes.");
				output=standardOutput+taskError.Result;
				return process.ExitCode;
			}
		}

		///<summary>Runs a driver that loads the named functions of the script and its script-level settings ($Marker*, $Record*Prefix,
		///$*FolderName, $*Suffix) from its syntax tree, without running the script, with Stop-Refused throwing 'REFUSED: ...' instead of
		///ending the process; then the body, with $Script and $Folder (this test's temporary folder). Asserts exit code 0; returns the output.
		///</summary>
		private string RunDriver(string[] arrayFunctions,string[] arrayBody) {
			string driver=Path.Combine(_folder,"driver-"+Guid.NewGuid().ToString("N").Substring(0,8)+".ps1");
			List<string> listLines=new List<string> {
				"param([string]$Script, [string]$Folder)",
				"$ErrorActionPreference = 'Stop'",
				"$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)",
				"$names = @('"+string.Join("', '",arrayFunctions)+"')",
				"foreach ($f in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $names -contains $a.Name }, $true)) {",
				"  . ([scriptblock]::Create($f.Extent.Text))",
				"}",
				"foreach ($v in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and $a.Left.Extent.Text -match '^\\$(Marker(Begin|End)|Record(Dev|Client|Parent)Prefix|(App|Dev|Client|Tls)FolderName|(Backup|IniTemp)Suffix)$' }, $false)) {",
				"  . ([scriptblock]::Create($v.Extent.Text))",
				"}",
				"function Stop-Refused([string]$Message) { throw ('REFUSED: ' + $Message) }",
				"$ServiceName = 'MariaDB'",
			};
			listLines.AddRange(arrayBody);
			File.WriteAllText(driver,string.Join("\r\n",listLines),Encoding.ASCII);
			int exitCode=RunPowerShell(driver,"-Script \""+GetScriptPath()+"\" -Folder \""+_folder+"\"",out string output);
			Assert.AreEqual(0,exitCode,output);
			return output;
		}

		private static bool IsElevated() {
			using(WindowsIdentity windowsIdentity=WindowsIdentity.GetCurrent()) {
				return new WindowsPrincipal(windowsIdentity).IsInRole(WindowsBuiltInRole.Administrator);
			}
		}

		private static byte[] ReadPem(string path,string label) {
			string text=File.ReadAllText(path);
			string begin="-----BEGIN "+label+"-----";
			string end="-----END "+label+"-----";
			int indexBegin=text.IndexOf(begin,StringComparison.Ordinal);
			int indexEnd=text.IndexOf(end,StringComparison.Ordinal);
			Assert.IsTrue(indexBegin==0 && indexEnd>indexBegin,path+" is one PEM block "+label);
			Assert.AreEqual(text.Length,indexEnd+end.Length+1,path+": nothing after the block but its line end.");
			return Convert.FromBase64String(text.Substring(indexBegin+begin.Length,indexEnd-indexBegin-begin.Length));
		}

		private static X509Extension GetExtension(X509Certificate2 x509Certificate2,string oid) {
			return x509Certificate2.Extensions.Cast<X509Extension>().FirstOrDefault(x => x.Oid.Value==oid);
		}

		///<summary>The DNS names and IP addresses of a Subject Alternative Name extension, read from its DER (GeneralNames: [2] dNSName, [7]
		///iPAddress), so the check does not depend on the language Windows formats extensions in.</summary>
		private static List<string> GetAlternativeNames(X509Extension x509Extension) {
			byte[] arrayBytes=x509Extension.RawData;
			List<string> listNames=new List<string>();
			Assert.AreEqual(0x30,arrayBytes[0],"GeneralNames is a SEQUENCE.");
			int index=2;
			while(index<arrayBytes.Length) {
				byte tag=arrayBytes[index];
				int length=arrayBytes[index+1];
				byte[] arrayValue=arrayBytes.Skip(index+2).Take(length).ToArray();
				if(tag==0x82) {
					listNames.Add("DNS:"+Encoding.ASCII.GetString(arrayValue));
				}
				else if(tag==0x87) {
					listNames.Add("IP:"+new System.Net.IPAddress(arrayValue).ToString());
				}
				else {
					listNames.Add("other:"+tag.ToString("X2"));
				}
				index+=2+length;
			}
			return listNames;
		}

		private static void AssertChainsTo(X509Certificate2 x509Certificate2Leaf,X509Certificate2 x509Certificate2Ca) {
			using(X509Chain x509Chain=new X509Chain()) {
				x509Chain.ChainPolicy.RevocationMode=X509RevocationMode.NoCheck;
				x509Chain.ChainPolicy.VerificationFlags=X509VerificationFlags.AllowUnknownCertificateAuthority;
				x509Chain.ChainPolicy.ExtraStore.Add(x509Certificate2Ca);
				Assert.IsTrue(x509Chain.Build(x509Certificate2Leaf),x509Certificate2Leaf.Subject+": "
					+string.Join(", ",x509Chain.ChainStatus.Select(x => x.Status)));
				Assert.AreEqual(2,x509Chain.ChainElements.Count);
				Assert.AreEqual(x509Certificate2Ca.Thumbprint,x509Chain.ChainElements[1].Certificate.Thumbprint,"The test CA is the root.");
			}
		}

		///<summary>The PKCS#8 key file belongs to the certificate: same modulus, and a signature with it verifies with the certificate.</summary>
		private static void AssertKeyMatches(string pathKey,X509Certificate2 x509Certificate2) {
			using(CngKey cngKey=CngKey.Import(ReadPem(pathKey,"PRIVATE KEY"),CngKeyBlobFormat.Pkcs8PrivateBlob))
			using(RSACng rsaCng=new RSACng(cngKey))
			using(RSA rsaPublic=x509Certificate2.GetRSAPublicKey())
			{
				Assert.IsTrue(rsaCng.KeySize>=2048,"RSA 2048 or more.");
				CollectionAssert.AreEqual(rsaPublic.ExportParameters(false).Modulus,rsaCng.ExportParameters(false).Modulus,pathKey);
				byte[] arrayData=Encoding.ASCII.GetBytes("O-12");
				byte[] arraySignature=rsaCng.SignData(arrayData,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
				Assert.IsTrue(rsaPublic.VerifyData(arrayData,arraySignature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
			}
		}

		[TestMethod]
		public void Script_IsAscii() {
			foreach(string name in new[] { "Enable-LocalDbTls.ps1","DevDbTls.ps1" }) {
				byte[] arrayBytes=File.ReadAllBytes(Path.Combine(GetRepoRoot(),"tools",name));
				Assert.IsFalse(arrayBytes.Any(x => x>0x7F),name+" is ASCII (Windows PowerShell 5.1 reads a file without a BOM as ANSI).");
			}
		}

		[TestMethod]
		public void Script_TestCertificates_ACa_AServerCertificateForLocalhostAnd127001_AndAClientCertificateOnlyWhenAskedFor() {
			string folder=Path.Combine(_folder,"certs");
			int exitCode=RunPowerShell(GetScriptPath(),"-TestCertificatesFolder \""+folder+"\" -WithClientCertificate",out string output);
			Assert.AreEqual(0,exitCode,output);
			CollectionAssert.AreEquivalent(new[] { "ca.pem","client-cert.pem","client-key.pem","server-cert.pem","server-key.pem" },
				Directory.GetFiles(folder).Select(x => Path.GetFileName(x)).ToArray(),"No file with the CA's private key.");
			X509Certificate2 x509Certificate2Ca=new X509Certificate2(ReadPem(Path.Combine(folder,"ca.pem"),"CERTIFICATE"));
			X509Certificate2 x509Certificate2Server=new X509Certificate2(ReadPem(Path.Combine(folder,"server-cert.pem"),"CERTIFICATE"));
			X509Certificate2 x509Certificate2Client=new X509Certificate2(ReadPem(Path.Combine(folder,"client-cert.pem"),"CERTIFICATE"));
			//The CA: a CA that signs certificates only, ten years.
			X509BasicConstraintsExtension basicCa=(X509BasicConstraintsExtension)GetExtension(x509Certificate2Ca,"2.5.29.19");
			Assert.IsTrue(basicCa.CertificateAuthority && basicCa.HasPathLengthConstraint && basicCa.PathLengthConstraint==0 && basicCa.Critical);
			X509KeyUsageExtension usageCa=(X509KeyUsageExtension)GetExtension(x509Certificate2Ca,"2.5.29.15");
			Assert.AreEqual(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,usageCa.KeyUsages);
			Assert.AreEqual(x509Certificate2Ca.Subject,x509Certificate2Ca.Issuer,"Self-signed.");
			Assert.IsTrue(x509Certificate2Ca.NotAfter>DateTime.Now.AddYears(9));
			Assert.IsFalse(x509Certificate2Ca.HasPrivateKey);
			//The server certificate: localhost and 127.0.0.1, server authentication, signed by the CA with SHA-256, five years.
			foreach(X509Certificate2 x509Certificate2 in new[] { x509Certificate2Server,x509Certificate2Client }) {
				Assert.AreEqual(x509Certificate2Ca.Subject,x509Certificate2.Issuer);
				Assert.AreEqual("1.2.840.113549.1.1.11",x509Certificate2.SignatureAlgorithm.Value,"sha256WithRSAEncryption");
				Assert.IsFalse(((X509BasicConstraintsExtension)GetExtension(x509Certificate2,"2.5.29.19")).CertificateAuthority);
				Assert.AreEqual(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,
					((X509KeyUsageExtension)GetExtension(x509Certificate2,"2.5.29.15")).KeyUsages);
				Assert.IsNotNull(GetExtension(x509Certificate2,"2.5.29.35"),"Authority Key Identifier.");
				Assert.IsTrue(x509Certificate2.NotAfter>DateTime.Now.AddYears(4) && x509Certificate2.NotAfter<=x509Certificate2Ca.NotAfter);
				Assert.IsTrue(x509Certificate2.NotBefore<DateTime.Now);
				AssertChainsTo(x509Certificate2,x509Certificate2Ca);
			}
			CollectionAssert.AreEqual(new[] { "DNS:localhost","IP:127.0.0.1" },GetAlternativeNames(GetExtension(x509Certificate2Server,"2.5.29.17")));
			Assert.AreEqual("1.3.6.1.5.5.7.3.1",((X509EnhancedKeyUsageExtension)GetExtension(x509Certificate2Server,"2.5.29.37"))
				.EnhancedKeyUsages.Cast<Oid>().Single().Value,"Server authentication only.");
			Assert.AreEqual("1.3.6.1.5.5.7.3.2",((X509EnhancedKeyUsageExtension)GetExtension(x509Certificate2Client,"2.5.29.37"))
				.EnhancedKeyUsages.Cast<Oid>().Single().Value,"Client authentication only (S2-2's certificate login).");
			StringAssert.StartsWith(x509Certificate2Client.Subject,"CN=opms-dbtls-test-client");
			AssertKeyMatches(Path.Combine(folder,"server-key.pem"),x509Certificate2Server);
			AssertKeyMatches(Path.Combine(folder,"client-key.pem"),x509Certificate2Client);
			Assert.IsFalse(File.ReadAllText(Path.Combine(folder,"ca.pem")).Contains("PRIVATE KEY"),"The CA file is its certificate only.");
			//A second run makes a new CA: nothing is reused. Without -WithClientCertificate (the default, until the owner agrees to a client
			//certificate) there is no client certificate and no client key.
			string folder2=Path.Combine(_folder,"certs2");
			Assert.AreEqual(0,RunPowerShell(GetScriptPath(),"-TestCertificatesFolder \""+folder2+"\"",out string output2),output2);
			Assert.AreNotEqual(x509Certificate2Ca.Thumbprint,new X509Certificate2(ReadPem(Path.Combine(folder2,"ca.pem"),"CERTIFICATE")).Thumbprint);
			CollectionAssert.AreEquivalent(new[] { "ca.pem","server-cert.pem","server-key.pem" },
				Directory.GetFiles(folder2).Select(x => Path.GetFileName(x)).ToArray(),"No client certificate by default.");
		}

		[TestMethod]
		public void Script_ClientCertificateSwitches_OnlyWithTheEnableRun() {
			//Refused before anything else (elevation, the service), so these run anywhere and change nothing.
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-Revert -WithClientCertificate",out string outputRevert),outputRevert);
			StringAssert.Contains(outputRevert,"-WithClientCertificate goes with the enable run");
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-RequireSecureTransport -WithClientCertificate",out string outputRequire),outputRequire);
			StringAssert.Contains(outputRequire,"-WithClientCertificate goes with the enable run");
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-ClientFolder \""+Path.Combine(_folder,"client")+"\"",out string outputClient),outputClient);
			StringAssert.Contains(outputClient,"-ClientFolder needs -WithClientCertificate");
			Assert.IsFalse(Directory.Exists(Path.Combine(_folder,"client")));
		}

		[TestMethod]
		public void Script_TestCertificatesFolder_InTheRepositoryOrNotEmpty_IsRefused() {
			string folderRepo=Path.Combine(GetRepoRoot(),"tools","dbtls-test-"+Guid.NewGuid().ToString("N").Substring(0,8));
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-TestCertificatesFolder \""+folderRepo+"\"",out string output),output);
			StringAssert.Contains(output,"inside the repository");
			Assert.IsFalse(Directory.Exists(folderRepo),"Nothing written.");
			File.WriteAllText(Path.Combine(_folder,"other.txt"),"x");
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-TestCertificatesFolder \""+_folder+"\"",out string outputNotEmpty),outputNotEmpty);
			StringAssert.Contains(outputNotEmpty,"is not empty");
			Assert.AreEqual(1,Directory.GetFiles(_folder).Length);
		}

		[TestMethod]
		public void Script_WithoutElevation_RefusesEveryChange() {
			if(IsElevated()) {
				Assert.Inconclusive("The test process is elevated; this test only runs without elevation, so the script can never change the server.");
			}
			foreach(string arguments in new[] { "","-RequireSecureTransport","-Revert" }) {
				Assert.AreEqual(1,RunPowerShell(GetScriptPath(),arguments,out string output),arguments+": "+output);
				StringAssert.Contains(output,"must run elevated",arguments);
				Assert.IsFalse(output.Contains("Plan:"),arguments+": it stops before it even makes a plan.");
			}
			Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-Revert -RequireSecureTransport",out string outputTwo),outputTwo);
			StringAssert.Contains(outputTwo,"one of");
		}

		[TestMethod]
		public void Script_DryRun_ShowsThePlan_AndChangesNothing() {
			int exitCode=RunPowerShell(GetScriptPath(),"-DryRun",out string output);
			if(output.Contains("was not found on this computer") || output.Contains("No connection to 127.0.0.1:3306")) {
				Assert.Inconclusive("No local MariaDB service on 127.0.0.1:3306: "+output);
			}
			Assert.AreEqual(0,exitCode,output);
			string lineIni=output.Split('\n').Select(x => x.Trim()).First(x => x.StartsWith("Option file:"));
			string pathIni=lineIni.Substring("Option file:".Length).Trim();
			Assert.IsTrue(File.Exists(pathIni),pathIni);
			if(output.Contains("already on through this script")) {
				StringAssert.Contains(output,"Nothing to do.");
			}
			else {
				StringAssert.Contains(output,"Plan:");
				StringAssert.Contains(output,"ssl_ca=");
				StringAssert.Contains(output,"tls_version=TLSv1.2,TLSv1.3");
				Assert.IsFalse(output.Contains("require_secure_transport=ON"),"Enabling never sets require_secure_transport.");
				StringAssert.Contains(output,"Dry run: nothing was changed.");
				Assert.IsFalse(File.Exists(pathIni+".before-opms-dev-tls"),"No backup was made.");
				//By default no client certificate and no key anywhere; the block records the dev folder for -Revert.
				Assert.IsFalse(output.Contains("client-key.pem"),"No client key without -WithClientCertificate.");
				StringAssert.Contains(output,"No private key goes to this folder.");
				StringAssert.Contains(output,"# dev-folder="+Path.GetDirectoryName(DevDbTls.GetDefaultCaPath()).Replace('\\','/'));
				Assert.IsFalse(output.Contains("# client-folder="));
				//With -WithClientCertificate the client key goes to a folder of its own, never the CA's folder.
				Assert.AreEqual(0,RunPowerShell(GetScriptPath(),"-DryRun -WithClientCertificate",out string outputClient),outputClient);
				string folderClient=DevDbTls.GetDefaultClientFolder();
				StringAssert.Contains(outputClient,Path.Combine(folderClient,"client-key.pem"));
				StringAssert.Contains(outputClient,"# client-folder="+folderClient.Replace('\\','/'));
				Assert.IsFalse(outputClient.Contains(Path.Combine(Path.GetDirectoryName(DevDbTls.GetDefaultCaPath()),"client-key.pem")),
					"Never next to the CA copy.");
				//The listener on 127.0.0.1:3306 is the service's own process. The dev folder gets a protected ACL with full control for the dev
				//user, and the block records the parent folder exactly when the run creates it.
				StringAssert.Contains(output,"the service's own");
				StringAssert.Contains(output,"Administrators full control, nobody else.");
				Assert.IsFalse(output.Contains("readable by"),"The dev user gets full control of the dev folder.");
				string folderParent=Path.GetDirectoryName(Path.GetDirectoryName(DevDbTls.GetDefaultCaPath()));
				Assert.AreEqual(!Directory.Exists(folderParent),output.Contains("# created-parent="+folderParent.Replace('\\','/')),
					"The created parent is recorded exactly when the run creates it: "+folderParent);
				//Folders an enable run refuses before any plan: the MariaDB folder, the drive root, the user profile, a folder outside a profile's
				//OpenPmsDental folder, a client folder of another name; -Revert refuses such a -DevFolder as well.
				string lineExe=output.Split('\n').Select(x => x.Trim()).First(x => x.StartsWith("Server program:"));
				string folderHome=Path.GetDirectoryName(Path.GetDirectoryName(lineExe.Substring("Server program:".Length).Trim()));
				foreach(string[] arrayCase in new[] {
					new[] { "-DevFolder \""+folderHome+"\"","is or overlaps the MariaDB folder" },
					new[] { "-DevFolder \""+Path.Combine(folderHome,"data","DevDbTls")+"\"","is or overlaps the MariaDB folder" },
					new[] { "-DevFolder \""+Path.GetPathRoot(_folder)+".\"","is a drive root" },
					new[] { "-DevFolder \""+Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)+"\"","is or holds the user profile" },
					new[] { "-DevFolder \""+Path.Combine(_folder,"OpenPmsDental","DevDbTls")+"\"",@"is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls" },
					new[] { "-WithClientCertificate -ClientFolder \""+Path.Combine(folderParent,"Other")+"\"",
						@"is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTlsClient" },
					new[] { "-Revert -DevFolder \""+Path.Combine(_folder,"OpenPmsDental","DevDbTls")+"\"",@"is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls" },
				})
				{
					Assert.AreEqual(1,RunPowerShell(GetScriptPath(),"-DryRun "+arrayCase[0],out string outputRefused),arrayCase[0]+": "+outputRefused);
					StringAssert.Contains(outputRefused,"REFUSED: ",arrayCase[0]);
					StringAssert.Contains(outputRefused,arrayCase[1],arrayCase[0]);
					Assert.IsFalse(outputRefused.Contains("Plan:"),arrayCase[0]+": refused before the plan.");
				}
				Assert.IsFalse(Directory.Exists(Path.Combine(_folder,"OpenPmsDental")),"Nothing was written.");
			}
			//The dry runs of the other modes change nothing either.
			byte[] arrayIniBefore=File.ReadAllBytes(pathIni);
			RunPowerShell(GetScriptPath(),"-DryRun -RequireSecureTransport",out string _);
			RunPowerShell(GetScriptPath(),"-DryRun -Revert",out string _);
			CollectionAssert.AreEqual(arrayIniBefore,File.ReadAllBytes(pathIni),"my.ini is unchanged.");
		}

		[TestMethod]
		public void Script_OptionFileEdit_AddsTheBlockToMysqld_KeepsEveryOtherByte_AndRemovesItAgain() {
			//A driver loads the script's own functions (its syntax tree, without running the script) and edits synthetic option files.
			string driver=Path.Combine(_folder,"driver.ps1");
			File.WriteAllText(driver,string.Join("\r\n",new[] {
				"param([string]$Script, [string]$Folder)",
				"$ErrorActionPreference = 'Stop'",
				"$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)",
				"$names = @('Stop-Refused', 'Read-OptionFile', 'Get-ServerOptions', 'Test-HasBlock', 'Remove-Block', 'Add-ToMysqldSection')",
				"foreach ($f in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $names -contains $a.Name }, $true)) {",
				"  . ([scriptblock]::Create($f.Extent.Text))",
				"}",
				"foreach ($v in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and $a.Left.Extent.Text -match '^\\$Marker(Begin|End)$' }, $false)) {",
				"  . ([scriptblock]::Create($v.Extent.Text))",
				"}",
				"$block = @($MarkerBegin, 'ssl_ca=C:/x/ca.pem', 'ssl_cert=C:/x/server-cert.pem', 'ssl_key=C:/x/server-key.pem', 'tls_version=TLSv1.2,TLSv1.3', $MarkerEnd)",
				"foreach ($name in @('lf.ini', 'crlf.ini', 'noend.ini', 'bom.ini')) {",
				"  $file = Read-OptionFile (Join-Path $Folder $name)",
				"  $new = Add-ToMysqldSection $file.Text $block $file.Newline",
				"  [System.IO.File]::WriteAllText((Join-Path $Folder ($name + '.new')), $new, (New-Object System.Text.UTF8Encoding($false)))",
				"  [System.IO.File]::WriteAllText((Join-Path $Folder ($name + '.removed')), (Remove-Block $new), (New-Object System.Text.UTF8Encoding($false)))",
				"  $options = Get-ServerOptions $new",
				"  'OPTIONS ' + $name + ' ' + $options['ssl_ca'] + ' ' + $options['tls_version'] + ' ' + (Test-HasBlock $new) + ' ' + $options.ContainsKey('require_secure_transport') + ' ' + $file.Encoding.GetPreamble().Length",
				"}",
			}),Encoding.ASCII);
			string ini="[mysqld]\ndatadir=C:/Program Files/MariaDB 10.11/data\nport=3306\nbind-address=127.0.0.1\nsql_mode=''\n\n[client]\nport=3306\n";
			File.WriteAllText(Path.Combine(_folder,"lf.ini"),ini,new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(_folder,"crlf.ini"),ini.Replace("\n","\r\n"),new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(_folder,"noend.ini"),"[mysqld]\nport=3306",new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(_folder,"bom.ini"),ini,new UTF8Encoding(true));
			int exitCode=RunPowerShell(driver,"-Script \""+GetScriptPath()+"\" -Folder \""+_folder+"\"",out string output);
			Assert.AreEqual(0,exitCode,output);
			foreach(string name in new[] { "lf.ini","crlf.ini","bom.ini" }) {
				StringAssert.Contains(output,"OPTIONS "+name+" C:/x/ca.pem TLSv1.2,TLSv1.3 True False");
			}
			StringAssert.Contains(output,"OPTIONS bom.ini C:/x/ca.pem TLSv1.2,TLSv1.3 True False 3","The BOM is kept.");
			string newLf=File.ReadAllText(Path.Combine(_folder,"lf.ini.new"));
			Assert.AreEqual(ini.Replace("sql_mode=''\n","sql_mode=''\n"+string.Join("\n",new[] {
				"# BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)","ssl_ca=C:/x/ca.pem",
				"ssl_cert=C:/x/server-cert.pem","ssl_key=C:/x/server-key.pem","tls_version=TLSv1.2,TLSv1.3","# END Open PMS - Dental local test TLS" })+"\n"),
				newLf,"At the end of [mysqld], before its blank line and [client].");
			Assert.AreEqual(newLf.Replace("\n","\r\n"),File.ReadAllText(Path.Combine(_folder,"crlf.ini.new")),"CRLF lines keep CRLF.");
			foreach(string name in new[] { "lf.ini","crlf.ini","bom.ini" }) {
				Assert.AreEqual(File.ReadAllText(Path.Combine(_folder,name)).TrimStart('\uFEFF'),File.ReadAllText(Path.Combine(_folder,name+".removed")),
					name+": removing the block gives the original back.");
			}
			StringAssert.StartsWith(File.ReadAllText(Path.Combine(_folder,"noend.ini.new")),"[mysqld]\nport=3306\n# BEGIN");
		}

		[TestMethod]
		public void Script_TheBlockRecordsTheFolders_RevertAndRequireSecureTransportUseThem_AndRefuseAnotherFolder() {
			//As the option-file test: the script's own functions on synthetic text. The enable run wrote the CA copy and the client pair for
			//another account (-DevUser, -DevFolder); the later modes must find them there, not in the folders of the account that runs them.
			string driver=Path.Combine(_folder,"driver.ps1");
			File.WriteAllText(driver,string.Join("\r\n",new[] {
				"param([string]$Script, [string]$Folder)",
				"$ErrorActionPreference = 'Stop'",
				"$ast = [System.Management.Automation.Language.Parser]::ParseFile($Script, [ref]$null, [ref]$null)",
				"$names = @('Resolve-FullPath', 'Read-OptionFile', 'Get-ServerOptions', 'Remove-Block', 'Add-ToMysqldSection', 'Get-NormalPath',",
				"  'Get-DefaultClientFolder', 'Test-FolderOverlap', 'Get-RecordedFolders', 'Resolve-RecordedFolders', 'Get-BlockLines', 'Get-DevFiles',",
				"  'Get-CreatedParents')",
				"foreach ($f in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $names -contains $a.Name }, $true)) {",
				"  . ([scriptblock]::Create($f.Extent.Text))",
				"}",
				"foreach ($v in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and $a.Left.Extent.Text -match '^\\$(Marker(Begin|End)|Record(Dev|Client|Parent)Prefix|ClientFolderName)$' }, $false)) {",
				"  . ([scriptblock]::Create($v.Extent.Text))",
				"}",
				"function Stop-Refused([string]$Message) { throw ('REFUSED: ' + $Message) }",
				"$dev = Join-Path $Folder 'other-account\\OpenPmsDental\\DevDbTls'",
				"$client = Get-DefaultClientFolder $dev",
				"'DEFAULTCLIENT ' + $client",
				"'OVERLAP ' + (Test-FolderOverlap $dev (Join-Path $dev 'client')) + ' ' + (Test-FolderOverlap (Join-Path $dev 'client') $dev) + ' ' + (Test-FolderOverlap $dev $client)",
				"$paths = [pscustomobject]@{ DevFolder = $dev; ClientFolder = $client; DevCa = (Join-Path $dev 'ca.pem'); ClientCert = (Join-Path $client 'client-cert.pem');",
				"  ClientKey = (Join-Path $client 'client-key.pem'); Ca = 'C:\\x\\ca.pem'; ServerCert = 'C:\\x\\server-cert.pem'; ServerKey = 'C:\\x\\server-key.pem' }",
				"$file = Read-OptionFile (Join-Path $Folder 'my.ini')",
				"$new = Add-ToMysqldSection $file.Text (Get-BlockLines $paths $false) $file.Newline",
				"[System.IO.File]::WriteAllText((Join-Path $Folder 'my.ini.new'), $new, (New-Object System.Text.UTF8Encoding($false)))",
				"'OPTIONS ' + (@((Get-ServerOptions $new).Keys | Sort-Object) -join ',')",
				"$recorded = Get-RecordedFolders $new",
				"'RECORDED ' + $recorded.DevFolder + '|' + $recorded.ClientFolder",
				"$resolved = Resolve-RecordedFolders $recorded '' '' (Join-Path $Folder 'this-account\\DevDbTls')",
				"'RESOLVED ' + $resolved.DevFolder + '|' + $resolved.ClientFolder + '|' + $resolved.IsRecorded",
				"'FILES ' + ((Get-DevFiles $paths) -join '|')",
				"'SAME ' + (Resolve-RecordedFolders $recorded ($dev.ToUpperInvariant() + '\\') ($client + '\\') 'x').DevFolder",
				"try { [void](Resolve-RecordedFolders $recorded (Join-Path $Folder 'this-account\\DevDbTls') '' 'x'); 'DEV not refused' } catch { 'DEV ' + $_.Exception.Message }",
				"try { [void](Resolve-RecordedFolders $recorded '' (Join-Path $Folder 'elsewhere') 'x'); 'CLIENT not refused' } catch { 'CLIENT ' + $_.Exception.Message }",
				"$paths.ClientFolder = ''",
				"$recordedNoClient = Get-RecordedFolders (Add-ToMysqldSection $file.Text (Get-BlockLines $paths $true) $file.Newline)",
				"'NOCLIENT ' + $recordedNoClient.DevFolder + '|' + ($null -eq $recordedNoClient.ClientFolder) + '|' + (Resolve-RecordedFolders $recordedNoClient '' '' 'x').ClientFolder",
				"try { [void](Resolve-RecordedFolders $recordedNoClient '' (Join-Path $Folder 'elsewhere') 'x'); 'CLIENT2 not refused' } catch { 'CLIENT2 ' + $_.Exception.Message }",
				"$none = Resolve-RecordedFolders (Get-RecordedFolders $file.Text) '' '' (Join-Path $Folder 'this-account\\DevDbTls')",
				"'NORECORD ' + $none.DevFolder + '|' + $none.ClientFolder + '|' + $none.IsRecorded",
				"'REMOVED ' + ($null -eq (Get-RecordedFolders (Remove-Block $new)).DevFolder) + ' ' + ((Remove-Block $new) -eq $file.Text)",
			}),Encoding.ASCII);
			string ini="[mysqld]\ndatadir=C:/Program Files/MariaDB 10.11/data\nport=3306\nbind-address=127.0.0.1\n\n[client]\nport=3306\n";
			File.WriteAllText(Path.Combine(_folder,"my.ini"),ini,new UTF8Encoding(false));
			int exitCode=RunPowerShell(driver,"-Script \""+GetScriptPath()+"\" -Folder \""+_folder+"\"",out string output);
			Assert.AreEqual(0,exitCode,output);
			string dev=Path.Combine(_folder,"other-account","OpenPmsDental","DevDbTls");
			string client=Path.Combine(_folder,"other-account","OpenPmsDental","DevDbTlsClient");
			StringAssert.Contains(output,"DEFAULTCLIENT "+client+"\r\n","DevDbTlsClient beside the dev folder.");
			Assert.AreEqual(Path.GetFileName(DevDbTls.GetDefaultClientFolder()),Path.GetFileName(client),"The script's default and DevDbTls agree.");
			StringAssert.Contains(output,"OVERLAP True True False");
			//The block: the record as comment lines (no options of the server), then the TLS options.
			string textNew=File.ReadAllText(Path.Combine(_folder,"my.ini.new"));
			StringAssert.Contains(textNew,"# BEGIN Open PMS - Dental local test TLS (tools/Enable-LocalDbTls.ps1; undo with -Revert)\n# dev-folder="
				+dev.Replace('\\','/')+"\n# client-folder="+client.Replace('\\','/')+"\nssl_ca=C:/x/ca.pem\n");
			StringAssert.Contains(output,"OPTIONS bind_address,datadir,port,ssl_ca,ssl_cert,ssl_key,tls_version\r\n");
			StringAssert.Contains(output,"RECORDED "+dev+"|"+client+"\r\n");
			StringAssert.Contains(output,"RESOLVED "+dev+"|"+client+"|True\r\n","The recorded folders, not this account's.");
			StringAssert.Contains(output,"FILES "+Path.Combine(dev,"ca.pem")+"|"+Path.Combine(client,"client-cert.pem")+"|"+Path.Combine(client,"client-key.pem")+"\r\n");
			StringAssert.Contains(output,"SAME "+dev+"\r\n","The same folder, written another way, is accepted.");
			StringAssert.Contains(output,"DEV REFUSED: my.ini records the dev folder "+dev);
			StringAssert.Contains(output,"CLIENT REFUSED: my.ini records the client folder "+client);
			StringAssert.Contains(output,"NOCLIENT "+dev+"|True|\r\n");
			StringAssert.Contains(output,"CLIENT2 REFUSED: my.ini records no client folder");
			StringAssert.Contains(output,"NORECORD "+Path.Combine(_folder,"this-account","DevDbTls")+"||False\r\n","No block: the fallback, marked as such.");
			StringAssert.Contains(output,"REMOVED True True");
		}

		[TestMethod]
		public void Script_EnableFolders_OnlyAProfilesOwnFolder_WithNothingButTheScriptsFiles_AwayFromTheServersFolders() {
			//The script's Get-FolderProblem on synthetic folders: profiles under the test's folder, a synthetic MariaDB folder, data folder and
			//certificate folder. Nothing outside the test's folder is created.
			string output=RunDriver(new[] { "Get-NormalPath","Get-CanonicalPath","Test-FolderOverlap","Test-ProfileFolder","Get-FolderProblem","Get-LocalAppDataRoots" },
				new[] {
				"$local = Join-Path $Folder 'profiles\\alice\\AppData\\Local'",
				"New-Item -ItemType Directory -Path $local | Out-Null",
				"$carol = Join-Path $Folder 'profiles\\carol\\AppData\\Local'",
				"New-Item -ItemType Directory -Path (Join-Path $carol 'OpenPmsDental') | Out-Null",
				"Set-Content -LiteralPath (Join-Path $carol 'OpenPmsDental\\DevDbTls') 'a file'",
				"$roots = @($local, (Join-Path $Folder 'profiles\\bob\\AppData\\Local'), $carol)",
				"$protected = [ordered]@{ 'the MariaDB folder' = (Join-Path $Folder 'mariadb'); \"the server's data folder\" = (Join-Path $Folder 'data'); 'the certificate folder' = (Join-Path $Folder 'tls') }",
				"$dev = Join-Path $local 'OpenPmsDental\\DevDbTls'",
				"$client = Join-Path $local 'OpenPmsDental\\DevDbTlsClient'",
				"function Show([string]$Case, [string]$Path, [string]$Name, [string[]]$Own, $Guard = $protected) {",
				"  $problem = Get-FolderProblem '-DevFolder' $Path $Name $Own $Guard $roots",
				"  if ($null -eq $problem) { $problem = 'OK' }",
				"  'CASE ' + $Case + ': ' + $problem",
				"}",
				"Show 'new' $dev 'DevDbTls' @('ca.pem')",
				"New-Item -ItemType Directory -Path $dev | Out-Null",
				"Set-Content -LiteralPath (Join-Path $dev 'ca.pem') 'x'",
				"Show 'own' $dev 'DevDbTls' @('ca.pem')",
				"Set-Content -LiteralPath (Join-Path $dev 'notes.txt') 'x'",
				"Show 'foreign' $dev 'DevDbTls' @('ca.pem')",
				"Remove-Item -LiteralPath (Join-Path $dev 'notes.txt')",
				"New-Item -ItemType Directory -Path (Join-Path $dev 'sub') | Out-Null",
				"Show 'subfolder' $dev 'DevDbTls' @('ca.pem')",
				"New-Item -ItemType Directory -Path $client | Out-Null",
				"foreach ($n in @('client-cert.pem', 'client-key.pem')) { Set-Content -LiteralPath (Join-Path $client $n) 'x' }",
				"Show 'clientown' $client 'DevDbTlsClient' @('client-cert.pem', 'client-key.pem')",
				"Set-Content -LiteralPath (Join-Path $client 'ca.pem') 'x'",
				"Show 'clientca' $client 'DevDbTlsClient' @('client-cert.pem', 'client-key.pem')",
				"Show 'driveroot' ([System.IO.Path]::GetPathRoot($Folder)) 'DevDbTls' @('ca.pem')",
				"Show 'profile' (Join-Path $Folder 'profiles\\alice') 'DevDbTls' @('ca.pem')",
				"Show 'profiles' (Join-Path $Folder 'profiles') 'DevDbTls' @('ca.pem')",
				"Show 'home' (Join-Path $Folder 'mariadb') 'DevDbTls' @('ca.pem')",
				"Show 'inhome' (Join-Path $Folder 'mariadb\\DevDbTls') 'DevDbTls' @('ca.pem')",
				"Show 'datadir' (Join-Path $Folder 'data\\DevDbTls') 'DevDbTls' @('ca.pem')",
				"Show 'tlsfolder' (Join-Path $Folder 'tls') 'DevDbTls' @('ca.pem')",
				"Show 'holdsdata' (Join-Path $Folder 'profiles\\alice\\AppData\\Local\\OpenPmsDental\\DevDbTlsClient') 'DevDbTlsClient' @('client-cert.pem', 'client-key.pem') ([ordered]@{ \"the server's data folder\" = (Join-Path $client 'data') })",
				"Show 'othername' (Join-Path $local 'OpenPmsDental\\Other') 'DevDbTls' @('ca.pem')",
				"Show 'wrongkind' $client 'DevDbTls' @('ca.pem')",
				"Show 'dotdot' ($dev + '\\..\\..\\..\\..\\..\\..\\elsewhere\\OpenPmsDental\\DevDbTls') 'DevDbTls' @('ca.pem')",
				"Show 'relative' 'OpenPmsDental\\DevDbTls' 'DevDbTls' @('ca.pem')",
				"Show 'nolocal' (Join-Path $Folder 'profiles\\bob\\AppData\\Local\\OpenPmsDental\\DevDbTls') 'DevDbTls' @('ca.pem')",
				"Show 'file' (Join-Path $carol 'OpenPmsDental\\DevDbTls') 'DevDbTls' @('ca.pem')",
				"$own = [Environment]::GetFolderPath('LocalApplicationData')",
				"'REALROOTS ' + ((Get-LocalAppDataRoots) -contains (Get-CanonicalPath $own))",
				"'REALDEFAULT ' + (Test-ProfileFolder (Join-Path $own 'OpenPmsDental\\DevDbTls') 'DevDbTls' (Get-LocalAppDataRoots)) + ' ' + (Test-ProfileFolder (Join-Path $own 'OpenPmsDental\\DevDbTlsClient') 'DevDbTlsClient' (Get-LocalAppDataRoots))",
			});
			string dev=Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental","DevDbTls");
			string client=Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental","DevDbTlsClient");
			StringAssert.Contains(output,"CASE new: OK\r\n","A new folder in a profile's OpenPmsDental folder.");
			StringAssert.Contains(output,"CASE own: OK\r\n","A folder with the script's own file only.");
			StringAssert.Contains(output,"CASE foreign: -DevFolder "+dev+" exists and holds notes.txt, which this script did not write");
			StringAssert.Contains(output,"CASE subfolder: -DevFolder "+dev+" exists and holds sub,");
			StringAssert.Contains(output,"CASE clientown: OK\r\n");
			StringAssert.Contains(output,"CASE clientca: -DevFolder "+client+" exists and holds ca.pem,","The client folder never holds the CA copy either.");
			StringAssert.Contains(output,"CASE driveroot: -DevFolder "+Path.GetPathRoot(_folder)+" is a drive root.");
			StringAssert.Contains(output,"CASE profile: -DevFolder "+Path.Combine(_folder,"profiles","alice")+" is or holds the user profile");
			StringAssert.Contains(output,"CASE profiles: -DevFolder "+Path.Combine(_folder,"profiles")+" is or holds the user profile");
			StringAssert.Contains(output,"CASE home: -DevFolder "+Path.Combine(_folder,"mariadb")+" is or overlaps the MariaDB folder");
			StringAssert.Contains(output,"CASE inhome: -DevFolder "+Path.Combine(_folder,"mariadb","DevDbTls")+" is or overlaps the MariaDB folder");
			StringAssert.Contains(output,"CASE datadir: -DevFolder "+Path.Combine(_folder,"data","DevDbTls")+" is or overlaps the server's data folder");
			StringAssert.Contains(output,"CASE tlsfolder: -DevFolder "+Path.Combine(_folder,"tls")+" is or overlaps the certificate folder");
			StringAssert.Contains(output,"CASE holdsdata: -DevFolder "+client+" is or overlaps the server's data folder","A server folder inside it.");
			StringAssert.Contains(output,"CASE othername: -DevFolder "+Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental","Other")
				+@" is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls");
			StringAssert.Contains(output,"CASE wrongkind: -DevFolder "+client+@" is not <a user profile>\AppData\Local\OpenPmsDental\DevDbTls");
			StringAssert.Contains(output,"CASE dotdot: -DevFolder "+Path.Combine(_folder,"elsewhere","OpenPmsDental","DevDbTls")+" is not","Judged by its full form.");
			StringAssert.Contains(output,@"CASE relative: -DevFolder OpenPmsDental\DevDbTls is not a full path");
			StringAssert.Contains(output,"CASE nolocal: -DevFolder "+Path.Combine(_folder,"profiles","bob","AppData","Local","OpenPmsDental","DevDbTls")
				+" is in "+Path.Combine(_folder,"profiles","bob","AppData","Local")+", which does not exist");
			StringAssert.Contains(output,"CASE file: -DevFolder "+Path.Combine(_folder,"profiles","carol","AppData","Local","OpenPmsDental","DevDbTls")+" is a file.");
			StringAssert.Contains(output,"REALROOTS True","This account's %LOCALAPPDATA% is one of the roots.");
			StringAssert.Contains(output,"REALDEFAULT True True","The defaults of DevDbTls pass.");
		}

		[TestMethod]
		public void Script_RecordedFolders_OnlyAProfilesOwnFolders_ForRevertAndRequireSecureTransport() {
			//The record in my.ini decides where -Revert removes files, so a record that no enable run can have written is refused: a folder outside
			//a profile's OpenPmsDental folder, another name, a path with .. segments, a relative path, a created parent that is not the parent.
			string output=RunDriver(new[] { "Resolve-FullPath","Get-NormalPath","Get-CanonicalPath","Test-ProfileFolder","Get-RecordProblem","Get-RecordedFolders",
				"Resolve-RecordedFolders","Add-ToMysqldSection","Get-BlockLines","Get-CreatedParents","Get-LocalAppDataRoots","Get-Paths","Get-DefaultClientFolder" },
				new[] {
				"$local = Join-Path $Folder 'profiles\\alice\\AppData\\Local'",
				"$roots = @($local)",
				"$dev = Join-Path $local 'OpenPmsDental\\DevDbTls'",
				"$client = Join-Path $local 'OpenPmsDental\\DevDbTlsClient'",
				"$parent = Join-Path $local 'OpenPmsDental'",
				"function Block([string]$D, [string]$C, [string[]]$P) {",
				"  $paths = [pscustomobject]@{ DevFolder = $D; ClientFolder = $C; CreatedParents = $P; Ca = 'C:\\x\\ca.pem'; ServerCert = 'C:\\x\\s.pem'; ServerKey = 'C:\\x\\k.pem' }",
				"  return Add-ToMysqldSection \"[mysqld]`nport=3306`n\" (Get-BlockLines $paths $false) \"`n\"",
				"}",
				"function Check([string]$Case, [string]$Text, [string]$GivenDev = '') {",
				"  $folders = Resolve-RecordedFolders (Get-RecordedFolders $Text) $GivenDev '' $dev",
				"  $problem = Get-RecordProblem $folders $roots",
				"  if ($null -eq $problem) { $problem = 'OK ' + $folders.DevFolder + '|' + $folders.ClientFolder + '|' + (@($folders.CreatedParents) -join ',') + '|' + $folders.IsRecorded }",
				"  'CASE ' + $Case + ': ' + $problem",
				"}",
				"Check 'good' (Block $dev $client @($parent))",
				"Check 'elsewhere' (Block (Join-Path $Folder 'elsewhere\\DevDbTls') '' @())",
				"Check 'othername' (Block (Join-Path $local 'OpenPmsDental\\Other') '' @())",
				"Check 'dotdot' (Block ($dev + '\\..\\..\\..\\..\\..\\..\\elsewhere\\OpenPmsDental\\DevDbTls') '' @())",
				"Check 'clientelsewhere' (Block $dev (Join-Path $Folder 'elsewhere\\DevDbTlsClient') @())",
				"Check 'clientname' (Block $dev $dev @())",
				"Check 'parentwrong' (Block $dev '' @((Join-Path $Folder 'profiles')))",
				"Check 'relative' (\"[mysqld]`n\" + $MarkerBegin + \"`n# dev-folder=OpenPmsDental/DevDbTls`n\" + $MarkerEnd + \"`n\")",
				"Check 'noblock' \"[mysqld]`nport=3306`n\"",
				"Check 'noblockgiven' \"[mysqld]`nport=3306`n\" (Join-Path $Folder 'elsewhere\\DevDbTls')",
				"$script:Facts = [pscustomobject]@{ Home = (Join-Path $Folder 'mariadb'); Ini = (Join-Path $Folder 'my.ini') }",
				"$DevFolder = ''",
				"$ClientFolder = ''",
				"$WithClientCertificate = $false",
				"$real = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'OpenPmsDental\\DevDbTls'",
				"$script:IniFile = [pscustomobject]@{ Text = (Block $real '' @((Split-Path -Parent $real))) }",
				"$got = Get-Paths $false",
				"'GETPATHS ' + $got.DevCa + '|' + $got.IsRecorded + '|' + (@($got.CreatedParents) -join ',')",
				"$script:IniFile = [pscustomobject]@{ Text = (Block (Join-Path $Folder 'elsewhere\\DevDbTls') '' @()) }",
				"try { [void](Get-Paths $false); 'GETPATHS2 not refused' } catch { 'GETPATHS2 ' + $_.Exception.Message }",
			});
			string dev=Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental","DevDbTls");
			string client=Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental","DevDbTlsClient");
			string parent=Path.Combine(_folder,"profiles","alice","AppData","Local","OpenPmsDental");
			StringAssert.Contains(output,"CASE good: OK "+dev+"|"+client+"|"+parent+"|True\r\n","The record of an enable run, read back with its created parent.");
			StringAssert.Contains(output,"CASE elsewhere: The dev folder "+Path.Combine(_folder,"elsewhere","DevDbTls")+", from the record in my.ini, is not");
			StringAssert.Contains(output,"CASE othername: The dev folder "+Path.Combine(parent,"Other")+", from the record in my.ini, is not");
			StringAssert.Contains(output,"CASE dotdot: The dev folder "+dev+@"\..\..\..\..\..\..\elsewhere\OpenPmsDental\DevDbTls, from the record in my.ini, is not");
			StringAssert.Contains(output,"CASE clientelsewhere: The client folder "+Path.Combine(_folder,"elsewhere","DevDbTlsClient")+", from the record in my.ini, is not");
			StringAssert.Contains(output,"CASE clientname: The client folder "+dev+", from the record in my.ini, is not","A client folder named DevDbTls.");
			StringAssert.Contains(output,"CASE parentwrong: my.ini records the created parent folder "+Path.Combine(_folder,"profiles")+", which is not the parent");
			StringAssert.Contains(output,@"CASE relative: The dev folder OpenPmsDental\DevDbTls, from the record in my.ini, is not");
			StringAssert.Contains(output,"CASE noblock: OK "+dev+"|||False\r\n","No block: the default folder of the fallback.");
			StringAssert.Contains(output,"CASE noblockgiven: The dev folder "+Path.Combine(_folder,"elsewhere","DevDbTls")+", from -DevFolder (or this account's default), is not");
			//Get-Paths, as -Revert and -RequireSecureTransport call it: a record in this account's own %LOCALAPPDATA% is taken (nothing is touched
			//there: Get-Paths only builds paths), a record elsewhere is refused.
			string real=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenPmsDental","DevDbTls");
			StringAssert.Contains(output,"GETPATHS "+Path.Combine(real,"ca.pem")+"|True|"+Path.GetDirectoryName(real)+"\r\n");
			StringAssert.Contains(output,"GETPATHS2 REFUSED: The dev folder "+Path.Combine(_folder,"elsewhere","DevDbTls")+", from the record in my.ini, is not");
			StringAssert.Contains(output,"This script does not touch it");
		}

		[TestMethod]
		public void Script_TheListenerOn3306_IsTheServicesOwnProcess() {
			//Get-ListenerProblem on synthetic listeners, then the real listener of this computer against the service's process (read only).
			string output=RunDriver(new[] { "Get-ListenerProblem","Get-Listeners","Get-ServiceProcessId" },new[] {
				"function L([string]$A, [int]$P) { [pscustomobject]@{ Address = $A; ProcessId = $P; ProcessName = 'p' + $P } }",
				"function Show([string]$Case, $Listeners, [int]$ServicePid) {",
				"  $problem = Get-ListenerProblem $Listeners $ServicePid",
				"  if ($null -eq $problem) { $problem = 'OK' }",
				"  'CASE ' + $Case + ': ' + $problem",
				"}",
				"Show 'own' @((L '127.0.0.1' 100)) 100",
				"Show 'ownwildcard' @((L '127.0.0.1' 100), (L '0.0.0.0' 100)) 100",
				"Show 'none' @() 100",
				"Show 'other' @((L '127.0.0.1' 200)) 100",
				"Show 'wildcard' @((L '127.0.0.1' 100), (L '0.0.0.0' 200)) 100",
				"Show 'v6wildcard' @((L '127.0.0.1' 100), (L '::' 300)) 100",
				"Show 'onlywildcard' @((L '0.0.0.0' 100)) 100",
				"Show 'noprocess' @((L '127.0.0.1' 100)) 0",
				"if ($null -ne (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) { Show 'real' (Get-Listeners) (Get-ServiceProcessId) } else { 'CASE real: no service' }",
			});
			StringAssert.Contains(output,"CASE own: OK\r\n");
			StringAssert.Contains(output,"CASE ownwildcard: OK\r\n","The service's own process on every address too.");
			StringAssert.Contains(output,"CASE none: the service's process 100 does not listen on 127.0.0.1:3306 (nothing does)");
			StringAssert.Contains(output,"CASE other: process 200 (p200) listens on 127.0.0.1:3306, not the service's process 100");
			StringAssert.Contains(output,"CASE wildcard: process 200 (p200) also listens on 0.0.0.0 port 3306");
			StringAssert.Contains(output,"CASE v6wildcard: process 300 (p300) also listens on :: port 3306");
			StringAssert.Contains(output,"CASE onlywildcard: the service's process 100 does not listen on 127.0.0.1:3306");
			StringAssert.Contains(output,"CASE noprocess: the service MariaDB has no process");
			if(output.Contains("CASE real: no service")) {
				Assert.Inconclusive("No MariaDB service on this computer: the real listener was not checked.");
			}
			StringAssert.Contains(output,"CASE real: OK\r\n","The listener on 127.0.0.1:3306 is the MariaDB service's own process.");
		}

		[TestMethod]
		public void Script_OptionFile_NotUtf8_OrWithAnInclude_IsRefused() {
			string ini="[mysqld]\nport=3306\nbind-address=127.0.0.1\n";
			File.WriteAllBytes(Path.Combine(_folder,"ansi.ini"),Encoding.ASCII.GetBytes(ini+"# caf").Concat(new byte[] { 0xE9,0x0A }).ToArray());
			File.WriteAllText(Path.Combine(_folder,"utf16.ini"),ini,Encoding.Unicode);
			File.WriteAllText(Path.Combine(_folder,"utf8.ini"),ini+"# caf\u00E9\n",new UTF8Encoding(false));
			File.WriteAllText(Path.Combine(_folder,"bom.ini"),ini,new UTF8Encoding(true));
			File.WriteAllText(Path.Combine(_folder,"include.ini"),ini+"!include C:/x/other.cnf\n  !includedir C:/x/conf.d\n#!include a comment\n"
				+"; !include a comment too\n",new UTF8Encoding(false));
			string output=RunDriver(new[] { "Read-OptionFile","Get-IncludeLines" },new[] {
				"foreach ($name in @('ansi.ini', 'utf16.ini', 'utf8.ini', 'bom.ini', 'include.ini')) {",
				"  try { $file = Read-OptionFile (Join-Path $Folder $name); 'READ ' + $name + ' OK ' + $file.Encoding.GetPreamble().Length + ' ' + $file.Text.Length }",
				"  catch { 'READ ' + $name + ' ' + $_.Exception.Message }",
				"}",
				"$includes = @(Get-IncludeLines ([System.IO.File]::ReadAllText((Join-Path $Folder 'include.ini'))))",
				"'INCLUDES ' + $includes.Count + ' ' + ($includes -join '|')",
				"'INCLUDES2 ' + @(Get-IncludeLines ([System.IO.File]::ReadAllText((Join-Path $Folder 'utf8.ini')))).Count",
			});
			int offsetAnsi=Encoding.ASCII.GetByteCount(ini+"# caf");
			StringAssert.Contains(output,"READ ansi.ini REFUSED: "+Path.Combine(_folder,"ansi.ini")+" is not UTF-8 text: the byte at offset "+offsetAnsi+" (0xE9)");
			StringAssert.Contains(output,"READ utf16.ini REFUSED: "+Path.Combine(_folder,"utf16.ini")+" is not UTF-8 text: the byte at offset 0 (0xFF)");
			StringAssert.Contains(output,"READ utf8.ini OK 0 "+(ini.Length+7)+"\r\n","UTF-8 with a character beyond ASCII comes back unchanged.");
			StringAssert.Contains(output,"READ bom.ini OK 3 "+ini.Length+"\r\n");
			StringAssert.Contains(output,"READ include.ini OK 0");
			StringAssert.Contains(output,"INCLUDES 2 !include C:/x/other.cnf|!includedir C:/x/conf.d\r\n","Comments are not includes.");
			StringAssert.Contains(output,"INCLUDES2 0");
			StringAssert.Contains(TestHelpers.ReadRepoFile("tools","Enable-LocalDbTls.ps1"),"$includes = @(Get-IncludeLines $script:IniFile.Text)",
				"Every mode's checks (Assert-LocalServer) refuse an include.");
		}

		[TestMethod]
		public void Script_FailedEnableRun_PutsBackWhatWasThere_AndRemovesWhatItAdded_RevertRemovesTheCreatedParent() {
			//The script's Save-ExistingItems, Undo-EnableFiles and Remove-NewFiles on scratch folders; "Run" writes what an enable run writes
			//there (without the ACLs of an elevated run, which need administrators: it changes the dev folder's access rules instead).
			string output=RunDriver(new[] { "Get-DevFiles","Get-CreatedParents","Remove-EmptyFolder","Get-AccessSddl","Set-AccessSddl","Save-ExistingItems",
				"Undo-EnableFiles","Remove-NewFiles" },new[] {
				"function P([string]$Base, [string[]]$Created) {",
				"  $d = Join-Path $Base 'OpenPmsDental\\DevDbTls'",
				"  $c = Join-Path $Base 'OpenPmsDental\\DevDbTlsClient'",
				"  [pscustomobject]@{ DevFolder = $d; ClientFolder = $c; DevCa = (Join-Path $d 'ca.pem'); ClientCert = (Join-Path $c 'client-cert.pem');",
				"    ClientKey = (Join-Path $c 'client-key.pem'); CreatedParents = $Created; TlsFolder = (Join-Path $Folder 'mariadb\\opms-dev-tls');",
				"    IniTemp = (Join-Path $Folder 'my.ini.opms-new') }",
				"}",
				"function Run($Paths) {",
				"  New-Item -ItemType Directory -Path $Paths.TlsFolder -Force | Out-Null",
				"  Set-Content -LiteralPath (Join-Path $Paths.TlsFolder 'server-key.pem') 'key'",
				"  New-Item -ItemType Directory -Path $Paths.DevFolder -Force | Out-Null",
				"  [System.IO.File]::WriteAllText($Paths.DevCa, 'NEW CA')",
				"  New-Item -ItemType Directory -Path $Paths.ClientFolder -Force | Out-Null",
				"  [System.IO.File]::WriteAllText($Paths.ClientCert, 'NEW CERT')",
				"  [System.IO.File]::WriteAllText($Paths.ClientKey, 'NEW KEY')",
				"  $acl = Get-Acl -LiteralPath $Paths.DevFolder",
				"  $acl.SetAccessRuleProtection($true, $true)",
				"  Set-Acl -LiteralPath $Paths.DevFolder -AclObject $acl",
				"  [System.IO.File]::WriteAllText($Paths.IniTemp, 'partial')",
				"}",
				"function T([string]$Path) { if (Test-Path -LiteralPath $Path) { 'yes' } else { 'no' } }",
				//The access rules, without the auto-inherited flag (AI) that Windows sets on a list it propagated: the same rules either way.
				"function Get-Rules([string]$Path) { (Get-AccessSddl $Path) -replace '^D:(P?)(AI)?', 'D:$1' }",
				//A: the dev folder and the client folder were there, with ca.pem and client-key.pem from before.
				"$a = P (Join-Path $Folder 'a\\Local') @()",
				"New-Item -ItemType Directory -Path $a.DevFolder -Force | Out-Null",
				"[System.IO.File]::WriteAllText($a.DevCa, 'OLD CA')",
				"New-Item -ItemType Directory -Path $a.ClientFolder -Force | Out-Null",
				"[System.IO.File]::WriteAllText($a.ClientKey, 'OLD KEY')",
				"$folderAcl = Get-Rules $a.DevFolder",
				"$fileAcl = Get-Rules $a.DevCa",
				"$saved = Save-ExistingItems $a",
				"Run $a",
				"'A changed ' + ((Get-Rules $a.DevFolder) -ne $folderAcl)",
				"Undo-EnableFiles $a $saved",
				"'A ca=' + [System.IO.File]::ReadAllText($a.DevCa) + ' key=' + [System.IO.File]::ReadAllText($a.ClientKey) + ' cert=' + (T $a.ClientCert) + ' devacl=' + ((Get-Rules $a.DevFolder) -eq $folderAcl) + ' caacl=' + ((Get-Rules $a.DevCa) -eq $fileAcl) + ' tls=' + (T $a.TlsFolder) + ' temp=' + (T $a.IniTemp) + ' client=' + (T $a.ClientFolder)",
				"'A acl ' + $folderAcl + ' -> ' + (Get-Rules $a.DevFolder)",
				//B: nothing was there but AppData\Local: the run created OpenPmsDental for its folders.
				"$bLocal = Join-Path $Folder 'b\\Local'",
				"New-Item -ItemType Directory -Path $bLocal | Out-Null",
				"$b = P $bLocal @((Join-Path $bLocal 'OpenPmsDental'))",
				"$savedB = Save-ExistingItems $b",
				"Run $b",
				"Undo-EnableFiles $b $savedB",
				"'B saved=' + @($savedB.Files).Count + ' parent=' + (T (Join-Path $bLocal 'OpenPmsDental')) + ' local=' + (T $bLocal) + ' tls=' + (T $b.TlsFolder) + ' temp=' + (T $b.IniTemp)",
				//C and D: -Revert after a run that created OpenPmsDental removes it when it is empty, and keeps it when something else is in it.
				"Run $b",
				"Remove-Item -LiteralPath $b.IniTemp",
				"Remove-NewFiles $b",
				"'C parent=' + (T (Join-Path $bLocal 'OpenPmsDental')) + ' tls=' + (T $b.TlsFolder)",
				"Run $b",
				"Remove-Item -LiteralPath $b.IniTemp",
				"Set-Content -LiteralPath (Join-Path $bLocal 'OpenPmsDental\\other-program.txt') 'x'",
				"Remove-NewFiles $b",
				"'D parent=' + (T (Join-Path $bLocal 'OpenPmsDental')) + ' other=' + (T (Join-Path $bLocal 'OpenPmsDental\\other-program.txt')) + ' dev=' + (T $b.DevFolder) + ' client=' + (T $b.ClientFolder)",
			});
			StringAssert.Contains(output,"A changed True","The run changed the dev folder's access rules.");
			StringAssert.Contains(output,"A ca=OLD CA key=OLD KEY cert=no devacl=True caacl=True tls=no temp=no client=yes\r\n",
				"The files that were there are back with their bytes and access rules, the added certificate and the temporary my.ini are gone.");
			StringAssert.Contains(output,"B saved=0 parent=no local=yes tls=no temp=no\r\n","The parent the run created is removed again; AppData\\Local stays.");
			StringAssert.Contains(output,"C parent=no tls=no\r\n");
			StringAssert.Contains(output,"D parent=yes other=yes dev=no client=no\r\n");
		}
	}
}
