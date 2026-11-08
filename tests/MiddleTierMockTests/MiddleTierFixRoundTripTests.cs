// Middle Tier round trips for the product fixes of package S0-T of the .NET 10 plan (docs/specs/net10-plan.md), made because two ported legacy
// tests failed: MiddleTier_ParametersTest (the Middle Tier source rules) and MiddleTier_GetPrimaryKeysFromInsert (LabCaseProducts). Each call
// below goes through the mock Middle Tier, so its arguments and its result pass through DtoObject's XmlSerializer and DtoProcessor as on a real
// Middle Tier, and each test checks the rows the server wrote. Before the fixes: a LabCaseProduct could not be serialized at all (its
// GlidewellProduct enum has no members); EForms.Delete sent one of its two parameters and the server found no such method;
// LanguagePats.SaveTranslationEFormField and DocumentMiscs.DeleteOpenDentalUpdateFiles ran their SQL on the client, which refuses it; and
// DocumentMiscs.ReplaceForkRelease threw on a Middle Tier client. Signalods.RefreshTimed only had its parameter order fixed: its round trip shows
// it still returns a signal through the Middle Tier. Synthetic rows only.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using UnitTestsCore;

namespace MiddleTierMockTests {
	[TestClass]
	public class MiddleTierFixRoundTripTests {
		///<summary>Text with the characters XML escapes, for the string fields that travel.</summary>
		private const string TEXT_XML_SPECIAL="<b>Smith & Jones's</b>";
		///<summary>The payloads (serialized DTOs) the client sent during the test, in order.</summary>
		private List<string> _listPayloads;
		private HashSet<Guid> _hashSetPayloadGuids;

		[TestInitialize]
		public void SetupTest() {
			ScratchDb.BeginTest();
			_listPayloads=new List<string>();
			_hashSetPayloadGuids=new HashSet<Guid>();
			//A Query Monitor of this thread only (thread static) that records each payload the client sends to the Middle Tier.
			QueryMonitor queryMonitor=new QueryMonitor();
			queryMonitor.IsMonitoring=true;
			queryMonitor.Fired+=(args) => {
				if(!(args.Tag is DbQueryObj dbQueryObj) || !dbQueryObj.IsPayload) {
					return;
				}
				lock(_listPayloads) {
					if(_hashSetPayloadGuids.Add(dbQueryObj.GUID)) {
						_listPayloads.Add(dbQueryObj.Command);
					}
				}
			};
			QueryMonitor.Monitor=queryMonitor;
		}

		[TestCleanup]
		public void TearDownTest() {
			QueryMonitor.Monitor=null;//Back to the global monitor.
			ScratchDb.EndTest();//Back to the direct connection, so that no other test runs as a Middle Tier client.
		}

		///<summary>LabCaseProducts through the Middle Tier: insert, update, get and sync, with GlidewellProduct numbers the enum does not define (it
		///has no members). Each number reaches the row and comes back as it is, under the element name GlidewellProduct. An empty list travels
		///too.</summary>
		[TestMethod]
		public void MiddleTier_LabCaseProduct_InsertUpdateGetSync() {
			Patient patient=PatientT.CreatePatient("S0TLab");
			LabCase labCase=new LabCase();
			labCase.PatNum=patient.PatNum;
			labCase.Instructions="S0T lab case";
			long labCaseNum=LabCases.Insert(labCase);
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			LabCaseProduct labCaseProduct=new LabCaseProduct();
			labCaseProduct.LabCaseNum=labCaseNum;
			labCaseProduct.PatNum=patient.PatNum;
			labCaseProduct.GlidewellProduct=(EnumGlidewellProductName)0;
			labCaseProduct.Material="BruxZir";
			labCaseProduct.Shade="A1";
			labCaseProduct.ToothRange="8-9";
			labCaseProduct.Notes=TEXT_XML_SPECIAL;
			long labCaseProductNum=LabCaseProducts.Insert(labCaseProduct);
			Assert.AreNotEqual(0,labCaseProductNum);
			Assert.AreEqual(labCaseProductNum,labCaseProduct.LabCaseProductNum,"The client copy gets the key the server returned.");
			Dictionary<string,string> dictRow=GetRow("SELECT LabCaseNum,PatNum,GlidewellProduct,Material,Shade,ToothRange,Notes FROM labcaseproduct "
				+"WHERE LabCaseProductNum="+labCaseProductNum);
			Assert.AreEqual(labCaseNum.ToString(),dictRow["LabCaseNum"]);
			Assert.AreEqual(patient.PatNum.ToString(),dictRow["PatNum"]);
			Assert.AreEqual("0",dictRow["GlidewellProduct"]);
			Assert.AreEqual("BruxZir",dictRow["Material"]);
			Assert.AreEqual("A1",dictRow["Shade"]);
			Assert.AreEqual("8-9",dictRow["ToothRange"]);
			Assert.AreEqual(TEXT_XML_SPECIAL,dictRow["Notes"]);
			labCaseProduct.GlidewellProduct=(EnumGlidewellProductName)7;
			labCaseProduct.Material="Zirconia";
			LabCaseProducts.Update(labCaseProduct);
			dictRow=GetRow("SELECT GlidewellProduct,Material FROM labcaseproduct WHERE LabCaseProductNum="+labCaseProductNum);
			Assert.AreEqual("7",dictRow["GlidewellProduct"],"The server got the stored number.");
			Assert.AreEqual("Zirconia",dictRow["Material"]);
			Assert.IsTrue(GetPayloadsFor("OpenPmsDentalBusiness.LabCaseProducts.Update").Single().Contains("<GlidewellProduct>7</GlidewellProduct>"),
				"The number travels under the field's own name.");
			List<LabCaseProduct> listLabCaseProducts=LabCaseProducts.GetForLabCase(labCaseNum);
			Assert.AreEqual(1,listLabCaseProducts.Count);
			Assert.AreEqual(labCaseProductNum,listLabCaseProducts[0].LabCaseProductNum);
			Assert.AreEqual(7,(int)listLabCaseProducts[0].GlidewellProduct,"The number came back.");
			Assert.AreEqual("Zirconia",listLabCaseProducts[0].Material);
			Assert.AreEqual(TEXT_XML_SPECIAL,listLabCaseProducts[0].Notes);
			LabCaseProduct labCaseProductNew=new LabCaseProduct();
			labCaseProductNew.GlidewellProduct=(EnumGlidewellProductName)3;
			labCaseProductNew.Material="Acrylic";
			listLabCaseProducts.Add(labCaseProductNew);
			List<LabCaseProduct> listSaved=LabCaseProducts.Sync(labCaseNum,patient.PatNum,listLabCaseProducts);
			Assert.AreEqual(2,listSaved.Count);
			Assert.AreEqual(labCaseProductNum,listSaved[0].LabCaseProductNum);
			Assert.AreNotEqual(0,listSaved[1].LabCaseProductNum,"The new row came back with its key.");
			CollectionAssert.AreEqual(new[] { 7,3 },listSaved.Select(x => (int)x.GlidewellProduct).ToArray());
			dictRow=GetRow("SELECT LabCaseNum,PatNum,GlidewellProduct,Material FROM labcaseproduct WHERE LabCaseProductNum="+listSaved[1].LabCaseProductNum);
			Assert.AreEqual(labCaseNum.ToString(),dictRow["LabCaseNum"]);
			Assert.AreEqual(patient.PatNum.ToString(),dictRow["PatNum"]);
			Assert.AreEqual("3",dictRow["GlidewellProduct"]);
			Assert.AreEqual("Acrylic",dictRow["Material"]);
			List<LabCaseProduct> listEmpty=LabCaseProducts.GetForLabCase(0);
			Assert.IsNotNull(listEmpty,"An empty list travels too.");
			Assert.AreEqual(0,listEmpty.Count);
		}

		///<summary>EForms.Delete sends both of its parameters: the server finds the method and deletes the row.</summary>
		[TestMethod]
		public void MiddleTier_EFormsDelete_DeletesTheRow() {
			Patient patient=PatientT.CreatePatient("S0TEForm");
			EForm eForm=EFormT.CreateEForm(0,new List<EFormField>(),patient.PatNum,EnumEFormStatus.None,description:"S0T eForm");
			Assert.AreEqual("1",GetScalar("SELECT COUNT(*) FROM eform WHERE EFormNum="+eForm.EFormNum));
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			EForms.Delete(eForm.EFormNum,eForm.PatNum);
			Assert.AreEqual("0",GetScalar("SELECT COUNT(*) FROM eform WHERE EFormNum="+eForm.EFormNum));
			string payload=GetPayloadsFor("OpenPmsDentalBusiness.EForms.Delete").Single();
			Assert.IsTrue(payload.Contains("<long>"+eForm.EFormNum+"</long>"));
			Assert.IsTrue(payload.Contains("<long>"+patient.PatNum+"</long>"));
		}

		///<summary>An eForm field translation saved through the Middle Tier: inserted, left alone when unchanged, updated, then deleted by an empty
		///text; a language the office does not use saves nothing. A custom language (not a three-letter code) keeps the test independent of the
		///culture names of the computer.</summary>
		[TestMethod]
		public void MiddleTier_LanguagePatsSaveTranslationEFormField_InsertUpdateDelete() {
			const long eFormFieldDefNum=970001;
			const string language="Tahitian";
			PrefT.UpdateString(PrefName.LanguagesUsedByPatients,language);
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			LanguagePats.RefreshCache();
			Assert.IsTrue(LanguagePats.SaveTranslationEFormField(eFormFieldDefNum,language,"Ia ora na "+TEXT_XML_SPECIAL));
			string where="FROM languagepat WHERE EFormFieldDefNum="+eFormFieldDefNum;
			Assert.AreEqual("1",GetScalar("SELECT COUNT(*) "+where));
			Dictionary<string,string> dictRow=GetRow("SELECT Language,Translation "+where);
			Assert.AreEqual(language,dictRow["Language"]);
			Assert.AreEqual("Ia ora na "+TEXT_XML_SPECIAL,dictRow["Translation"]);
			LanguagePats.RefreshCache();//As the eForm field windows do after a change; through the Middle Tier it refreshes the server's cache too.
			Assert.IsFalse(LanguagePats.SaveTranslationEFormField(eFormFieldDefNum,language,"Ia ora na "+TEXT_XML_SPECIAL),"Unchanged: nothing saved.");
			Assert.IsTrue(LanguagePats.SaveTranslationEFormField(eFormFieldDefNum,language,"Maeva"));
			Assert.AreEqual("1",GetScalar("SELECT COUNT(*) "+where),"Updated in place, not inserted again.");
			Assert.AreEqual("Maeva",GetRow("SELECT Translation "+where)["Translation"]);
			LanguagePats.RefreshCache();
			Assert.IsTrue(LanguagePats.SaveTranslationEFormField(eFormFieldDefNum,language,""),"An empty text deletes the translation.");
			Assert.AreEqual("0",GetScalar("SELECT COUNT(*) "+where));
			Assert.IsFalse(LanguagePats.SaveTranslationEFormField(eFormFieldDefNum,"Klingon","x"),"A language the office does not use saves nothing.");
			Assert.AreEqual("0",GetScalar("SELECT COUNT(*) "+where));
			Assert.AreEqual(5,GetPayloadsFor("OpenPmsDentalBusiness.LanguagePats.SaveTranslationEFormField").Count,"Each call went to the server.");
		}

		///<summary>DocumentMiscs.DeleteOpenDentalUpdateFiles through the Middle Tier: Open Dental's two update file types are deleted and counted,
		///another type is kept.</summary>
		[TestMethod]
		public void MiddleTier_DocumentMiscsDeleteOpenDentalUpdateFiles_DeletesOnlyThoseRows() {
			long countBefore=long.Parse(GetScalar("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN ("
				+(int)DocumentMiscType.UpdateFiles+","+(int)DocumentMiscType.UpdateFilesSegment+")"));
			long docMiscNumKept=InsertDocumentMisc(DocumentMiscType.ShareScreenExeSegment,"S0Tkept");
			InsertDocumentMisc(DocumentMiscType.UpdateFiles,"S0Tupdate");
			InsertDocumentMisc(DocumentMiscType.UpdateFilesSegment,"S0Tsegment");
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			long countDeleted=DocumentMiscs.DeleteOpenDentalUpdateFiles();
			Assert.AreEqual(countBefore+2,countDeleted);
			Assert.AreEqual("0",GetScalar("SELECT COUNT(*) FROM documentmisc WHERE DocMiscType IN ("
				+(int)DocumentMiscType.UpdateFiles+","+(int)DocumentMiscType.UpdateFilesSegment+")"));
			Assert.AreEqual("1",GetScalar("SELECT COUNT(*) FROM documentmisc WHERE DocMiscNum="+docMiscNumKept));
			Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.DocumentMiscs.DeleteOpenDentalUpdateFiles").Count);
			ScratchDb.UseDirectConnection();
			DocumentMiscs.DeleteMany(new List<long> { docMiscNumKept });
		}

		///<summary>DocumentMiscs.ReplaceForkRelease through the Middle Tier: the server replaces the manifest and the segments, which read back the
		///same through the Middle Tier.</summary>
		[TestMethod]
		public void MiddleTier_DocumentMiscsReplaceForkRelease_WritesManifestAndSegments() {
			string manifestBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes("S0T manifest"));
			List<string> listSegmentsBase64=new List<string> {
				Convert.ToBase64String(Encoding.UTF8.GetBytes("segment one")),
				Convert.ToBase64String(Encoding.UTF8.GetBytes("segment two")) };
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			try {
				DocumentMiscs.ReplaceForkRelease(manifestBase64,listSegmentsBase64);
				DocumentMisc documentMiscManifest=DocumentMiscs.GetForkReleaseManifest();
				Assert.IsNotNull(documentMiscManifest);
				Assert.AreEqual("ForkRelease.manifest",documentMiscManifest.FileName);
				Assert.AreEqual(manifestBase64,documentMiscManifest.RawBase64);
				List<DocumentMisc> listSegments=DocumentMiscs.GetForkReleaseSegments();
				CollectionAssert.AreEqual(new[] { "0001","0002" },listSegments.Select(x => x.FileName).ToArray());
				CollectionAssert.AreEqual(listSegmentsBase64,listSegments.Select(x => x.RawBase64).ToList());
				Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.DocumentMiscs.ReplaceForkRelease").Count);
			}
			finally {
				ScratchDb.UseDirectConnection();
				DocumentMiscs.DeleteAllForType(DocumentMiscType.ForkReleaseManifest);
				DocumentMiscs.DeleteAllForType(DocumentMiscType.ForkReleaseSegment);
			}
		}

		///<summary>Signalods.RefreshTimed through the Middle Tier after its parameter order fix: a signal from a few seconds ago is returned for a check
		///since a minute ago, and not for a check since now. The time the client sends is its own (no read-only server, so no look-back).</summary>
		[TestMethod]
		public void MiddleTier_SignalodsRefreshTimed_ReturnsTheSignal() {
			long signalNum;
			DateTime dateTNowServer;
			using(MySqlConnection connection=ScratchDb.OpenServer(ScratchDb.DbName)) {
				ScratchDb.Exec(connection,"INSERT INTO signalod (SigDateTime,FKey,FKeyType,IType,RemoteRole,MsgValue) "
					+"VALUES (NOW() - INTERVAL 5 SECOND,424242,'Undefined',"+(int)InvalidType.Providers+",0,'')");
				signalNum=long.Parse(ScratchDb.Scalar(connection,"SELECT LAST_INSERT_ID()"));
				using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT NOW()",connection)) {
					dateTNowServer=(DateTime)mySqlCommand.ExecuteScalar();
				}
			}
			List<InvalidType> listInvalidTypes=new List<InvalidType> { InvalidType.Providers };
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			List<Signalod> listSignalods=Signalods.RefreshTimed(dateTNowServer.AddMinutes(-1),listInvalidTypes);
			Signalod signalod=listSignalods.SingleOrDefault(x => x.SignalNum==signalNum);
			Assert.IsNotNull(signalod,"The signal of 5 seconds ago is returned.");
			Assert.AreEqual(424242L,signalod.FKey);
			Assert.AreEqual(InvalidType.Providers,signalod.IType);
			Assert.IsFalse(Signalods.RefreshTimed(dateTNowServer,listInvalidTypes).Any(x => x.SignalNum==signalNum),"Not returned for a check since now.");
			Assert.AreEqual(2,GetPayloadsFor("OpenPmsDentalBusiness.Signalods.RefreshTimed").Count);
		}

		///<summary>Inserts a documentmisc row over the direct connection. Returns its key.</summary>
		private static long InsertDocumentMisc(DocumentMiscType documentMiscType,string fileName) {
			DocumentMisc documentMisc=new DocumentMisc();
			documentMisc.DateCreated=new DateTime(2026,11,8);
			documentMisc.DocMiscType=documentMiscType;
			documentMisc.FileName=fileName;
			documentMisc.RawBase64=Convert.ToBase64String(Encoding.UTF8.GetBytes(fileName));
			return DocumentMiscs.Insert(documentMisc);
		}

		///<summary>The payloads of calls to the given method (namespace, class and method), in the order they were sent.</summary>
		private List<string> GetPayloadsFor(string methodName) {
			lock(_listPayloads) {
				return _listPayloads.FindAll(x => x.Contains("<MethodName>"+methodName+"</MethodName>"));
			}
		}

		///<summary>One value read straight from the scratch database, outside the business layer and the Middle Tier.</summary>
		private static string GetScalar(string command) {
			using(MySqlConnection connection=ScratchDb.OpenServer(ScratchDb.DbName)) {
				return ScratchDb.Scalar(connection,command);
			}
		}

		///<summary>One row read straight from the scratch database, outside the business layer and the Middle Tier. Column name to text.</summary>
		private static Dictionary<string,string> GetRow(string command) {
			Dictionary<string,string> dictRow=new Dictionary<string,string>();
			using(MySqlConnection connection=ScratchDb.OpenServer(ScratchDb.DbName))
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection))
			using(MySqlDataReader reader=mySqlCommand.ExecuteReader())
			{
				Assert.IsTrue(reader.Read(),"No row for: "+command);
				for(int i=0;i<reader.FieldCount;i++) {
					dictRow[reader.GetName(i)]=Convert.ToString(reader.GetValue(i),CultureInfo.InvariantCulture);
				}
			}
			return dictRow;
		}
	}
}
