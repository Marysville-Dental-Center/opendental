using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace NoHqTests {
	///<summary>Checks that the functions which downloaded or imported reference data from Open Dental are gone: the default medication list,
	///translations, Canadian procedure codes and fee schedules, code systems and the VeriFone USB driver. Per the owner, that data is loaded into
	///the database directly. The CPT import stays, because it reads the CPT data file that the office licenses from the AMA.
	///None of these tests needs a database or a network connection. The OpenDental.exe checks read the built program next to this project.</summary>
	[TestClass]
	public class RefDataTests {
		private const BindingFlags ALL_DECLARED=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance
			|BindingFlags.DeclaredOnly;

		[TestMethod]
		public void CodeSystems_OnlyTheCptImportRemains() {
			List<string> listImportNames=typeof(CodeSystems).GetMethods(ALL_DECLARED).Where(x => x.Name.StartsWith("Import")).Select(x => x.Name).ToList();
			CollectionAssert.AreEqual(new List<string> { "ImportCpt" },listImportNames);
			//Only the overload that takes the version of the imported CPT file. The other one copied the version that HQ said was available.
			Assert.AreEqual(1,typeof(CodeSystems).GetMethods(ALL_DECLARED).Count(x => x.Name=="UpdateCurrentVersion"));
		}

		[TestMethod]
		public void BusinessLayer_RefDataImportsAreGone() {
			AssertNoMembers(typeof(FeeScheds),"ImportCanadaFeeSchedule2");
			AssertNoMembers(typeof(Lans),"LoadTranslationsFromTextFile");
			AssertNoMembers(typeof(RxNorms),"IsRxNormTableSmall");
		}

		[TestMethod]
		public void OpenDental_RefDataDownloadsAreGone() {
			Assembly assembly=LoadBuiltAssembly(ProgramNames.ExeFileName);
			//The windows share the entry point's namespace. Taken from the assembly, so the checks below still find them after a rename, and
			//the IsNull checks cannot pass only because the namespace changed.
			string ns=TypeLookup.GetEntryNamespace(assembly);
			AssertNoMembers(FindType(assembly,ns+".MedicationL"),"DownloadDefaultMedicationsFile");
			AssertNoMembers(FindType(assembly,ns+".FormMedications"),"DownloadDefaultMedications");
			AssertNoMembers(FindType(assembly,ns+".FormTranslationCat"),"butDownload","butDownload_Click");
			AssertNoMembers(FindType(assembly,ns+".FormProcTools"),"CanadaDownloadProcedureCodes");
			AssertNoMembers(FindType(assembly,ns+".FormFeeSchedTools"),"butImportCanada","butImportCanada_Click");
			Assert.IsNull(assembly.GetType(ns+".FormFeeSchedPickRemote"),"FormFeeSchedPickRemote should be deleted.");
			Assert.IsNull(assembly.GetType(ns+".FormFeeSchedPickAuthOntario"),"FormFeeSchedPickAuthOntario should be deleted.");
			AssertNoMembers(FindType(assembly,ns+".FormPayConnectSetup"),"butDownloadDriver","butDownloadDriver_Click");
			Type typeFormCodeSystemsImport=FindType(assembly,ns+".FormCodeSystemsImport");
			AssertNoMembers(typeFormCodeSystemsImport,"butCheckUpdates","butDownload","checkKeepDescriptions","PreDownloadHelper","listEnumCodeSystemNames");
			Type typeThread=typeFormCodeSystemsImport.GetNestedType("UpdateCodeSystemThread",BindingFlags.NonPublic);
			Assert.IsNotNull(typeThread,"The CPT import thread class was not found.");
			AssertNoMembers(typeThread,"DownloadFileHelper","DownloadFileWorker","DownloadProgress","SendAndReceiveDownloadXml");
			Assert.IsNull(assembly.GetType(ns+".EnumCodeSystemName"),"EnumCodeSystemName should be deleted.");
			AssertNoMembers(FindType(assembly,ns+".FormIcd9s"),"butCodeImport","butCodeImport_Click");
			AssertNoMembers(FindType(assembly,ns+".FormIcd10s"),"butCodeImport","butCodeImport_Click");
			AssertNoMembers(FindType(assembly,ns+".FormRxNorms"),"FormRxNorms_Shown");
		}

		[TestMethod]
		public void WpfControls_CdcrecDownloadButtonIsGone() {
			Assembly assembly=LoadBuiltAssembly("WpfControlsOD.dll");
			string ns=TypeLookup.GetNamespaceOf(assembly,"FrmODBase");
			AssertNoMembers(FindType(assembly,ns+".FrmPatientRaceEthnicity"),"butImport","butImport_Click");
		}

		#region Helpers
		///<summary>Loads an assembly from OpenDental\bin\Debug (or Release), the folder the solution builds OpenDental.exe into. The test is inconclusive
		///when it has not been built.</summary>
		private static Assembly LoadBuiltAssembly(string fileName) {
			string dirTests=Path.GetDirectoryName(typeof(RefDataTests).Assembly.Location);//NoHqTests\bin\Debug
			string dirConfiguration=Path.GetFileName(dirTests);
			string path=Path.GetFullPath(Path.Combine(dirTests,"..","..","..","OpenDental","bin",dirConfiguration,fileName));
			if(!File.Exists(path)) {
				Assert.Inconclusive(fileName+" is not built: "+path);
			}
			return Assembly.LoadFrom(path);
		}

		private static Type FindType(Assembly assembly,string typeName) {
			Type type=assembly.GetType(typeName);
			Assert.IsNotNull(type,typeName+" was not found in "+assembly.GetName().Name+".");
			return type;
		}

		private static void AssertNoMembers(Type type,params string[] arrayMemberNames) {
			for(int i=0;i<arrayMemberNames.Length;i++) {
				Assert.AreEqual(0,type.GetMember(arrayMemberNames[i],ALL_DECLARED).Length,type.Name+"."+arrayMemberNames[i]+" should be removed.");
			}
		}
		#endregion Helpers
	}
}
