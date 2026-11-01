// CLAIM-DCM: a DICOM file the program cannot read is refused cleanly on every claim path: an ODException with a clear message the windows
// show, nothing rendered, built or written, and the file left as it was. The files are made up.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.IO;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace CLAIMDCMTests {
	[TestClass]
	public class MalformedDicomTests {
		private string _patFolder;

		[TestInitialize]
		public void SetUp() {
			TestSupport.UseLocalAtoZ();
			_patFolder=TestSupport.NewFolder();
		}

		[TestCleanup]
		public void TearDown() {
			TestSupport.DeleteFolder(_patFolder);
		}

		private static void AssertClearMessage(ODException ex,string fileName) {
			StringAssert.Contains(ex.Message,"could not be read, so it was not attached to the claim",fileName);
			StringAssert.Contains(ex.Message,fileName,"the message names the file");
		}

		[TestMethod]
		public void MalformedDcm_IsRefusedCleanly_OnEveryPath() {
			byte[][] arrayFiles=TestSupport.MalformedFiles();
			for(int i=0;i<arrayFiles.Length;i++) {
				string fileName="broken"+i+".dcm";
				Document document=TestSupport.SaveDocument(_patFolder,fileName,arrayFiles[i]);
				string path=Path.Combine(_patFolder,fileName);
				string hashBefore=TestSupport.Hash(path);
				//Edit Claim (FormImageSelectClaimAttach): no copy is built, so no claimattach row or file is made.
				AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder),fileName),fileName);
				//The DentalXChange image picker and the windows' preview read the document the same way.
				AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ReadDocument(document,_patFolder),fileName),fileName);
				//Send File, Add Image and Paste read the file from disk; Edit Claim's Import checks it before importing.
				AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ReadFile(path),fileName),fileName);
				Assert.AreEqual(hashBefore,TestSupport.Hash(path),"The file is left as it was.");
			}
		}

		[TestMethod]
		public void MalformedDcm_UpperCaseName_IsRefusedCleanly() {
			Document document=TestSupport.SaveDocument(_patFolder,"BROKEN.DCM",new byte[] { 0,1,2,3 });
			AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.BuildClaimAttachFile(document,_patFolder),"BROKEN.DCM"),"BROKEN.DCM");
		}

		[TestMethod]
		public void MissingOrEmptyDcm_IsRefusedCleanly() {
			Document documentMissing=new Document { FileName="gone.dcm",WindowingMax=255 };
			AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.BuildClaimAttachFile(documentMissing,_patFolder),"missing"),"gone.dcm");
			AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ReadFile(Path.Combine(_patFolder,"gone.dcm")),"missing"),"gone.dcm");
			Document documentEmpty=TestSupport.SaveDocument(_patFolder,"empty.dcm",new byte[0]);
			AssertClearMessage(TestSupport.AssertThrowsOD(() => ClaimAttachDicom.BuildClaimAttachFile(documentEmpty,_patFolder),"empty"),"empty.dcm");
			TestSupport.AssertThrowsOD(() => ClaimAttachDicom.ReadDocument(null,_patFolder),"no document");
			Assert.IsNull(ClaimAttachDicom.Render(null,null));
			Assert.IsNull(ClaimAttachDicom.RenderFile(null));
		}
	}
}
