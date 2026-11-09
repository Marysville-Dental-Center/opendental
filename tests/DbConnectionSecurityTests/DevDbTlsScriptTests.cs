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
				"  'Get-DefaultClientFolder', 'Test-FolderOverlap', 'Get-RecordedFolders', 'Resolve-RecordedFolders', 'Get-BlockLines', 'Get-DevFiles')",
				"foreach ($f in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $names -contains $a.Name }, $true)) {",
				"  . ([scriptblock]::Create($f.Extent.Text))",
				"}",
				"foreach ($v in $ast.FindAll({ param($a) $a -is [System.Management.Automation.Language.AssignmentStatementAst] -and $a.Left.Extent.Text -match '^\\$(Marker(Begin|End)|Record(Dev|Client)Prefix|ClientFolderName)$' }, $false)) {",
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
	}
}
