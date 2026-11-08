// Three Middle Tier round trips added by package S0-T of the .NET 10 plan (docs/specs/net10-plan.md, finding G1 and S3a-9 (iv)). On .NET 10,
// XmlSerializer serializes [Obsolete] members and throws while it builds a serializer for a type with a member marked [Obsolete(IsError=true)],
// enum constants included; PrefName has such members. The calls below go through the mock Middle Tier, so their arguments and results pass through
// DtoObject's XmlSerializer and DtoProcessor: a PrefName argument of a Meth call (Prefs.GetBoolNoCache), a ClinicPref, whose PrefName is a public
// field (insert, update and get), and an Adjustment, whose obsolete TaxTransID field the serializer leaves out on .NET Framework and carries on
// .NET 10. No exception is caught: a serializer that throws on either side fails the test. Each test also checks the payload the client sent
// (through a Query Monitor of its own thread) and the row the server wrote, so a value that silently did not arrive fails too.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.WebServices;
using UnitTestsCore;

namespace MiddleTierMockTests {
	[TestClass]
	public class ObsoleteMemberRoundTripTests {
		///<summary>Text with the characters XML escapes, for the string fields that travel.</summary>
		private const string TEXT_XML_SPECIAL="<a href=\"x\">Smith & Jones's</a>";
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
			//Back to the direct connection, so that no other test runs as a Middle Tier client.
			ScratchDb.EndTest();
		}

		///<summary>A Meth call with a PrefName argument. Two preferences with opposite values, read twice with the values swapped in between: only a
		///PrefName that arrived intact on the server gives all four answers.</summary>
		[TestMethod]
		public void MiddleTier_PrefNameArgument_GetBoolNoCache() {
			PrefT.UpdateBool(PrefName.EasyNoClinics,true);
			PrefT.UpdateBool(PrefName.EasyHidePublicHealth,false);
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			AssertCallsGoThroughTheMock();
			Assert.IsTrue(Prefs.GetBoolNoCache(PrefName.EasyNoClinics));
			Assert.IsFalse(Prefs.GetBoolNoCache(PrefName.EasyHidePublicHealth));
			ScratchDb.UseDirectConnection();
			PrefT.UpdateBool(PrefName.EasyNoClinics,false);
			PrefT.UpdateBool(PrefName.EasyHidePublicHealth,true);
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			Assert.IsFalse(Prefs.GetBoolNoCache(PrefName.EasyNoClinics));
			Assert.IsTrue(Prefs.GetBoolNoCache(PrefName.EasyHidePublicHealth));
			List<string> listPayloads=GetPayloadsFor("OpenPmsDentalBusiness.Prefs.GetBoolNoCache");
			Assert.AreEqual(4,listPayloads.Count,"Each call sends one payload.");
			Assert.IsTrue(listPayloads[0].Contains(">"+nameof(PrefName.EasyNoClinics)+"<"),"The PrefName argument is in the payload by name.");
			Assert.IsTrue(listPayloads[1].Contains(">"+nameof(PrefName.EasyHidePublicHealth)+"<"),"The PrefName argument is in the payload by name.");
		}

		///<summary>A ClinicPref (its PrefName is a public field) inserted, updated and read back through the Middle Tier.</summary>
		[TestMethod]
		public void MiddleTier_ClinicPref_InsertUpdateGet() {
			Clinic clinic=ClinicT.CreateClinic("S0T clinic");
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			AssertCallsGoThroughTheMock();
			ClinicPref clinicPref=new ClinicPref(clinic.ClinicNum,PrefName.EmailDisclaimerIsOn,"1");
			long clinicPrefNum=ClinicPrefs.Insert(clinicPref);
			Assert.AreNotEqual(0,clinicPrefNum);
			Assert.AreEqual(clinicPrefNum,clinicPref.ClinicPrefNum,"The client copy gets the key the server returned.");
			Dictionary<string,string> dictRow=GetRow("SELECT ClinicNum,PrefName,ValueString FROM clinicpref WHERE ClinicPrefNum="+clinicPrefNum);
			Assert.AreEqual(clinic.ClinicNum.ToString(),dictRow["ClinicNum"]);
			Assert.AreEqual(nameof(PrefName.EmailDisclaimerIsOn),dictRow["PrefName"],"The server got the PrefName field.");
			Assert.AreEqual("1",dictRow["ValueString"]);
			clinicPref.PrefName=PrefName.EasyNoClinics;
			clinicPref.ValueString=TEXT_XML_SPECIAL;
			ClinicPrefs.Update(clinicPref);
			dictRow=GetRow("SELECT ClinicNum,PrefName,ValueString FROM clinicpref WHERE ClinicPrefNum="+clinicPrefNum);
			Assert.AreEqual(nameof(PrefName.EasyNoClinics),dictRow["PrefName"],"The server got the changed PrefName field.");
			Assert.AreEqual(TEXT_XML_SPECIAL,dictRow["ValueString"]);
			ClinicPref clinicPrefFromServer=ClinicPrefs.GetOne(clinicPrefNum);
			Assert.IsNotNull(clinicPrefFromServer);
			Assert.AreEqual(clinicPrefNum,clinicPrefFromServer.ClinicPrefNum);
			Assert.AreEqual(clinic.ClinicNum,clinicPrefFromServer.ClinicNum);
			Assert.AreEqual(PrefName.EasyNoClinics,clinicPrefFromServer.PrefName,"The PrefName field came back.");
			Assert.AreEqual(TEXT_XML_SPECIAL,clinicPrefFromServer.ValueString);
			Assert.IsTrue(GetPayloadsFor("OpenPmsDentalBusiness.ClinicPrefs.Insert").Single().Contains(">"+nameof(PrefName.EmailDisclaimerIsOn)+"<"));
			Assert.IsTrue(GetPayloadsFor("OpenPmsDentalBusiness.ClinicPrefs.Update").Single().Contains(">"+nameof(PrefName.EasyNoClinics)+"<"));
			Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.ClinicPrefs.GetOne").Count);
		}

		///<summary>An Adjustment inserted, updated and read back through the Middle Tier. Its obsolete TaxTransID is set on the client: XmlSerializer
		///leaves the [Obsolete] field out on .NET Framework (the server writes 0), and carries it on .NET 10 (the server writes the value).</summary>
		[TestMethod]
		public void MiddleTier_Adjustment_InsertUpdateGet() {
			Patient patient=PatientT.CreatePatient("S0TAdj");
			bool isNetFramework=typeof(object).Assembly.GetName().Name=="mscorlib";
			long taxTransIdExpected=isNetFramework ? 0 : 4242;
			ScratchDb.UseMockMiddleTier(ScratchDb.NewMockServer());
			AssertCallsGoThroughTheMock();
			Adjustment adjustment=new Adjustment();
			adjustment.PatNum=patient.PatNum;
			adjustment.AdjAmt=12.34;
			adjustment.AdjDate=new DateTime(2026,11,2);
			adjustment.ProcDate=new DateTime(2026,10,30);
			adjustment.AdjNote=TEXT_XML_SPECIAL;
			adjustment.ProvNum=1;
#pragma warning disable CS0618 // The obsolete field is the point of this test.
			adjustment.TaxTransID=4242;
#pragma warning restore CS0618
			long adjNum=Adjustments.Insert(adjustment);
			Assert.AreNotEqual(0,adjNum);
			Assert.AreEqual(adjNum,adjustment.AdjNum,"The client copy gets the key the server returned.");
			Dictionary<string,string> dictRow=GetRow("SELECT PatNum,AdjAmt,AdjNote,ProvNum,SecUserNumEntry,TaxTransID FROM adjustment WHERE AdjNum="+adjNum);
			Assert.AreEqual(patient.PatNum.ToString(),dictRow["PatNum"]);
			Assert.AreEqual(12.34,double.Parse(dictRow["AdjAmt"],System.Globalization.CultureInfo.InvariantCulture),0.001);
			Assert.AreEqual(TEXT_XML_SPECIAL,dictRow["AdjNote"]);
			Assert.AreEqual("1",dictRow["ProvNum"]);
			Assert.AreEqual(Security.CurUser.UserNum.ToString(),dictRow["SecUserNumEntry"],"The server took the user from the credentials in the payload.");
			Assert.AreEqual(taxTransIdExpected.ToString(),dictRow["TaxTransID"],
				"TaxTransID is [Obsolete]: left out on .NET Framework, carried on .NET 10 (docs/specs/net10-plan.md, G1).");
			adjustment.AdjAmt=56.78;
			adjustment.AdjNote="Updated "+TEXT_XML_SPECIAL;
			Adjustments.Update(adjustment);
			Adjustment adjustmentFromServer=Adjustments.GetOne(adjNum);
			Assert.IsNotNull(adjustmentFromServer);
			Assert.AreEqual(adjNum,adjustmentFromServer.AdjNum);
			Assert.AreEqual(patient.PatNum,adjustmentFromServer.PatNum);
			Assert.AreEqual(56.78,adjustmentFromServer.AdjAmt,0.001);
			Assert.AreEqual("Updated "+TEXT_XML_SPECIAL,adjustmentFromServer.AdjNote);
			Assert.AreEqual(new DateTime(2026,11,2),adjustmentFromServer.AdjDate);
			Assert.AreEqual(new DateTime(2026,10,30),adjustmentFromServer.ProcDate);
#pragma warning disable CS0618 // The obsolete field is the point of this test.
			Assert.AreEqual(taxTransIdExpected,adjustmentFromServer.TaxTransID,
				"TaxTransID is [Obsolete]: left out on .NET Framework, carried on .NET 10 (docs/specs/net10-plan.md, G1).");
#pragma warning restore CS0618
			Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.Adjustments.Insert").Count);
			Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.Adjustments.Update").Count);
			Assert.AreEqual(1,GetPayloadsFor("OpenPmsDentalBusiness.Adjustments.GetOne").Count);
		}

		///<summary>This thread is a Middle Tier client and the proxy hands out the mock server: there is no other way for a Meth call to go.</summary>
		private static void AssertCallsGoThroughTheMock() {
			Assert.AreEqual(MiddleTierRole.ClientMT,RemotingClient.MiddleTierRole);
			Assert.IsInstanceOfType(OpenDentalServerProxy.GetOpenDentalServerInstance(),typeof(OpenDentalServerMockIIS));
		}

		///<summary>The payloads of calls to the given method (namespace, class and method), in the order they were sent.</summary>
		private List<string> GetPayloadsFor(string methodName) {
			lock(_listPayloads) {
				return _listPayloads.FindAll(x => x.Contains("<MethodName>"+methodName+"</MethodName>"));
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
					dictRow[reader.GetName(i)]=Convert.ToString(reader.GetValue(i),System.Globalization.CultureInfo.InvariantCulture);
				}
			}
			return dictRow;
		}
	}
}
