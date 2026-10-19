// Tests for package CANADA (docs/specs/vendor-direct/canada.md section 4.1, owner decision of 2026-10-18): the Canadian claim network services that
// Open Dental's HQ relayed are removed. The carrier list comes only from the office's own ITRANS Claims Director, and the CCDWS certificate check is
// read-only. None of these tests needs a database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.Eclaims;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.Canada {
	[TestClass]
	public class CanadaCleanupTests {
		private string _tempFolder;
		private CultureInfo _cultureOld;
		private CultureInfo _cultureUiOld;

		[TestInitialize]
		public void SetUp() {
			_tempFolder=Path.Combine(Path.GetTempPath(),"VendorDirectTests_Canada_"+Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempFolder);
			//Lans.g returns the English text without touching the database only under en-US.
			_cultureOld=Thread.CurrentThread.CurrentCulture;
			_cultureUiOld=Thread.CurrentThread.CurrentUICulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Thread.CurrentThread.CurrentUICulture=new CultureInfo("en-US");
		}

		[TestCleanup]
		public void TearDown() {
			Thread.CurrentThread.CurrentCulture=_cultureOld;
			Thread.CurrentThread.CurrentUICulture=_cultureUiOld;
			try {
				Directory.Delete(_tempFolder,true);
			}
			catch(Exception) {
				//A test process may still hold a file for a moment. The folder is in %TEMP%.
			}
		}

		private static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		///<summary>Removes // comments so that source checks look only at code.</summary>
		private static string StripLineComments(string source) {
			return string.Join("\n",source.Split('\n').Select(x => {
				int index=x.IndexOf("//");
				return index<0 ? x : x.Substring(0,index);
			}));
		}

		private static CanadianNetwork Network(string abbrev) {
			return new CanadianNetwork { Abbrev=abbrev,Descript=abbrev+" network" };
		}

		///<summary>Makes a CCDWS layout: the folder with ccdws.ini (unless iniText is null) and a network sub-folder holding the given .pem files.</summary>
		private string MakeCcdws(string subDir,string iniText,params string[] arrayPemFiles) {
			string networkFolder=Path.Combine(_tempFolder,subDir);
			Directory.CreateDirectory(networkFolder);
			foreach(string pem in arrayPemFiles) {
				File.WriteAllText(Path.Combine(networkFolder,pem),"dummy");
			}
			if(iniText!=null) {
				File.WriteAllText(Path.Combine(_tempFolder,"ccdws.ini"),iniText);
			}
			return networkFolder;
		}

		#region Carrier list (ItransNCpl)

		[TestMethod]
		public void CarrierList_NoHqRelayInTheSource() {
			string code=StripLineComments(ReadRepoFile(@"OpenDentBusiness\Misc\ItransNCpl.cs"));
			foreach(string forbidden in new[] { "WebServiceMainHQProxy","CanadaCarrierUpdate","PayloadHelper","HqNotAvailableException","WebSerializer" }) {
				Assert.IsFalse(code.Contains(forbidden),forbidden);
			}
			Assert.IsFalse(code.Contains("WaitForExit()"),"The --getncpl command must have a timeout.");
			StringAssert.Contains(code,"WaitForExit(timeoutMs)");
			Assert.AreEqual(120000,ItransNCpl.GetNcplTimeoutMs);
		}

		[TestMethod]
		public void CarrierList_NeedsItrans2WithAResponsePath() {
			Clearinghouse[] arrayClearinghouses={
				null,
				new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS,ResponsePath=_tempFolder },
				new Clearinghouse { CommBridge=EclaimsCommBridge.Claimstream,ResponsePath=_tempFolder },
				new Clearinghouse { CommBridge=EclaimsCommBridge.None,ResponsePath=_tempFolder },
				new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS2,ResponsePath="" },
				new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS2,ResponsePath="  " },
			};
			foreach(Clearinghouse clearinghouse in arrayClearinghouses) {
				string error=ItransNCpl.TryGetJsonFromIcd(clearinghouse,false,out string json,out DateTime _);
				StringAssert.Contains(error,"needs the ITRANS Claims Director (ITRANS 2.0)",clearinghouse?.CommBridge.ToString()??"null");
				Assert.AreEqual("",json);
			}
		}

		[TestMethod]
		public void CarrierList_MissingIcdExe_ReturnsMessageWithoutRunningAnything() {
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS2,ResponsePath=_tempFolder };
			string error=ItransNCpl.TryGetJsonFromIcd(clearinghouse,false,out string json,out DateTime _);
			StringAssert.Contains(error,"Unable to find 'ITRANS Claims Director.exe'");
			Assert.AreEqual("",json);
		}

		[TestMethod]
		public void CarrierList_ReadsTheFileTheLocalIcdWrote() {
			//Any console program that exits at once stands in for ITRANS Claims Director.exe --getncpl.
			string exeStandIn=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"hostname.exe");
			if(!File.Exists(exeStandIn)) {
				Assert.Inconclusive("hostname.exe not found.");
			}
			File.Copy(exeStandIn,Path.Combine(_tempFolder,"ITRANS Claims Director.exe"));
			string jsonExpected="{\"Carriers\":[],\"Rot_Bins\":[],\"Change_Log\":[]}";
			File.WriteAllText(Path.Combine(_tempFolder,"n-cpl.json"),jsonExpected);
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS2,ResponsePath=_tempFolder };
			string error=ItransNCpl.TryGetJsonFromIcd(clearinghouse,false,out string json,out DateTime _);
			Assert.AreEqual("",error);
			Assert.AreEqual(jsonExpected,json);
		}

		[TestMethod]
		public void CarrierList_MissingListFile_ReturnsMessageInsteadOfThrowing() {
			string exeStandIn=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"hostname.exe");
			if(!File.Exists(exeStandIn)) {
				Assert.Inconclusive("hostname.exe not found.");
			}
			File.Copy(exeStandIn,Path.Combine(_tempFolder,"ITRANS Claims Director.exe"));
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.ITRANS2,ResponsePath=_tempFolder };
			string error=ItransNCpl.TryGetJsonFromIcd(clearinghouse,false,out string json,out DateTime _);
			StringAssert.Contains(error,"Unable to read the carrier list file");
			Assert.AreEqual("",json);
		}

		[TestMethod]
		public void CarrierList_CommandThatCannotStart_ReturnsMessage() {
			string error=ItransNCpl.TryRunGetNcplCommand(Path.Combine(_tempFolder,"missing.exe"),1000);
			StringAssert.Contains(error,"Unable to run 'ITRANS Claims Director.exe'.");
		}

		[TestMethod]
		public void CarrierThread_StartsOnlyForItrans2WithAResponsePath() {
			string code=ReadRepoFile(@"OpenDental\Main Modules\FormOpenDentalThreads.cs");
			int start=code.IndexOf("private void BeginCanadianItransCarrierThread()");
			Assert.IsTrue(start>=0);
			string method=code.Substring(start,code.IndexOf("odThread.Start();",start)-start);
			StringAssert.Contains(method,"CommBridge!=EclaimsCommBridge.ITRANS2");
			StringAssert.Contains(method,"string.IsNullOrWhiteSpace(clearinghouseDefault.ResponsePath)");
		}

		#endregion
		#region Network certificates (Canadian.PassToIca)

		[TestMethod]
		public void Certificates_NoDownloadOrConfigRewriteInTheSource() {
			string code=StripLineComments(ReadRepoFile(@"OpenDentBusiness\Eclaims\Canadian.cs"));
			foreach(string forbidden in new[] { "CustomerUpdatesProxy","RequestCertCanada","RegistrationKeyIsDisabled","FileData64","OD_2023-","OD_2018-",
				"\"OPENDENTAL.pem\"","WriteAllBytes(certFilePath","WriteAllText(configFilePath" })
			{
				Assert.IsFalse(code.Contains(forbidden),forbidden);
			}
			StringAssert.Contains(code,"GetVendorCertificateError(network,saveFolder,configFilePath)");
		}

		[TestMethod]
		public void Certificates_OpenDentalNamesAreRecognized() {
			foreach(string name in new[] { "OPENDENTAL.pem","opendental.PEM","OD_2023-02-20_2028-02-24_prod.pem","OD_2023-02-05_2028-02-09_staging.pem",
				"OD_2018-05-17_2023-05-21_prod.pem"," OD_2018-02-26_2023-03-02_staging.pem " })
			{
				Assert.IsTrue(Canadian.IsOtherVendorCertificateName(name),name);
			}
			foreach(string name in new[] { null,"","acme_vendor.pem","OD_prod.pem","MYOPENDENTAL.pem","OD_2023-02-20_2028-02-24_test.pem","OPENDENTAL.pem.bak" }) {
				Assert.IsFalse(Canadian.IsOtherVendorCertificateName(name),name??"null");
			}
		}

		[TestMethod]
		public void Certificates_InstreamNeedsNoVendorCertificate() {
			string networkFolder=MakeCcdws("instream",null);
			Assert.AreEqual("",Canadian.GetVendorCertificateError(Network("CSI"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini")));
			Assert.AreEqual("",Canadian.GetVendorCertificateError(null,networkFolder,Path.Combine(_tempFolder,"ccdws.ini")));
		}

		[TestMethod]
		public void Certificates_MissingCcdwsIni_Fails() {
			string networkFolder=MakeCcdws("abc",null,"acme_vendor.pem");
			string error=Canadian.GetVendorCertificateError(Network("ABC"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"CCDWS is not configured for network ABC network");
		}

		[TestMethod]
		public void Certificates_NoCertificateInstalled_FailsWithTheVendorCertificateMessage() {
			//No .pem in the folder.
			string networkFolder=MakeCcdws("telusb","[telusb]\r\ncertificate=acme_vendor.pem\r\n");
			string error=Canadian.GetVendorCertificateError(Network("TELUS B"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"No software-vendor certificate is installed for network TELUS B network");
			StringAssert.Contains(error,"Dentist claims can be sent through ITRANS 2.0 instead.");
			//A .pem in the folder that ccdws.ini doesn't name doesn't count.
			File.WriteAllText(Path.Combine(networkFolder,"other.pem"),"dummy");
			error=Canadian.GetVendorCertificateError(Network("TELUS B"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"No software-vendor certificate is installed");
			//The template placeholder never passes.
			File.WriteAllText(Path.Combine(_tempFolder,"ccdws.ini"),"[telusb]\r\ncertificate=<vendor_prod_cert_filename>.pem\r\n");
			error=Canadian.GetVendorCertificateError(Network("TELUS B"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"No software-vendor certificate is installed");
		}

		[TestMethod]
		public void Certificates_OpenDentalCertificate_IsBlocked() {
			string networkFolder=MakeCcdws("telusa","[telusa]\r\ncertificate=OD_2023-02-20_2028-02-24_prod.pem\r\n","OD_2023-02-20_2028-02-24_prod.pem");
			string error=Canadian.GetVendorCertificateError(Network("TELUS A"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"belongs to another software vendor and can't be used by this program.");
			networkFolder=MakeCcdws("abc","[abc]\r\ncert = C:\\ccd\\abc\\OPENDENTAL.pem\r\n","OPENDENTAL.pem");
			error=Canadian.GetVendorCertificateError(Network("ABC"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini"));
			StringAssert.Contains(error,"belongs to another software vendor");
		}

		[TestMethod]
		public void Certificates_OfficesOwnVendorCertificate_Passes() {
			string iniText="[abc]\r\ncert=C:\\ccd\\abc\\acme_vendor.pem\r\n[telusb]\r\ncert=\"acme_vendor_telus.pem\"\r\n";
			string networkFolder=MakeCcdws("abc",iniText,"acme_vendor.pem");
			Assert.AreEqual("",Canadian.GetVendorCertificateError(Network("ABC"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini")));
			networkFolder=MakeCcdws("telusb",iniText,"acme_vendor_telus.pem");
			Assert.AreEqual("",Canadian.GetVendorCertificateError(Network("TELUS B"),networkFolder,Path.Combine(_tempFolder,"ccdws.ini")));
		}

		[TestMethod]
		public void Certificates_CheckIsReadOnly() {
			string iniText="[telusb]\r\ncertificate=OD_2023-02-05_2028-02-09_prod.pem\r\n";
			string networkFolder=MakeCcdws("telusb",iniText);
			string iniPath=Path.Combine(_tempFolder,"ccdws.ini");
			byte[] arrayIniBefore=File.ReadAllBytes(iniPath);
			string error=Canadian.GetVendorCertificateError(Network("TELUS B"),networkFolder,iniPath);
			Assert.AreNotEqual("",error);
			CollectionAssert.AreEqual(arrayIniBefore,File.ReadAllBytes(iniPath),"ccdws.ini must not be rewritten.");
			Assert.AreEqual(0,Directory.GetFiles(networkFolder).Length,"No certificate may be downloaded or created.");
		}

		[TestMethod]
		public void Certificates_PassToIca_WritesNoClaimFileWhenTheCheckFails() {
			string networkFolder=MakeCcdws("telusb","[telusb]\r\n");
			Clearinghouse clearinghouse=new Clearinghouse { CommBridge=EclaimsCommBridge.Claimstream,ExportPath=_tempFolder };
			string msgText="123456789012000001".PadRight(64,'0');//Field A02 (office sequence number) is characters 12-17.
			string response=Canadian.PassToIca(msgText,clearinghouse,Network("TELUS B"),false,out string errorMsg);
			Assert.AreEqual("",response);
			StringAssert.Contains(errorMsg,"No software-vendor certificate is installed");
			Assert.AreEqual(0,Directory.GetFiles(networkFolder).Length,"No input file may be written.");
		}

		#endregion
	}
}
