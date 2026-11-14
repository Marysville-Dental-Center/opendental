// Three CCDs that EhrCCD builds from synthetic patients, rendered through the stylesheet child and compared with recorded pages (package EHR-3 of
// docs/specs/ehr-rebuild-plan.md, docs/port-notes/EHR-3.md): a summary of care, a clinical summary and a patient export of a patient with a
// non-ASCII name. They need a database: a scratch database of their own on the local MariaDB (127.0.0.1:3306, root, blank password) whose name
// starts with ehr3_ plus this run's id, built from db/init/new-database.sql and the fork migrations as the EHR-1 helper builds its own, filled
// with synthetic rows (made-up names and places, a CVX code, CDCREC race codes, a TEST SNOMED row) and dropped at the end; leftovers older than
// 12 hours are dropped first, and only names starting with ehr3_. Without the server the tests are inconclusive (EHR_TEST_DB=skip skips them;
// EHR_KEEP_DB=1 keeps the database). After each test the business layer goes back to the EHR-1 scratch database, with its caches, when it exists.
// The identifiers and times a CCD gets when it is made (GUIDs, the time of the export, the computer's time zone) are replaced with fixed values
// before the document is rendered, so the pages compare.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace EhrTests {
	[TestClass]
	public class CdaGeneratedCcdTests {
		public const string PREFIX="ehr3_";
		private static readonly string _dbName=PREFIX+DateTime.UtcNow.ToString("yyMMddHHmmss",CultureInfo.InvariantCulture)+"_"
			+System.Diagnostics.Process.GetCurrentProcess().Id+"_"+new Random().Next(1000,10000)+"_ccd";
		private static readonly TimeSpan _timeSpanStale=TimeSpan.FromHours(12);
		private static readonly object _lockBuild=new object();
		private static bool _isBuildTried;
		private static bool _isBuilt;
		///<summary>Why the tests are inconclusive: no server, or EHR_TEST_DB=skip.</summary>
		private static string _skipReason;
		///<summary>Why the synthetic rows could not be made: the tests fail with it.</summary>
		private static string _buildError;
		private static long _patNumA;
		private static long _patNumB;
		private CultureInfo _cultureBefore;

		[TestInitialize]
		public void Begin() {
			lock(_lockBuild) {
				if(!_isBuildTried) {
					_isBuildTried=true;
					Build();
				}
			}
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			Assert.IsNull(_buildError,_buildError);
			//Lans.g returns the English text in en-US, and touches the language table in any other culture.
			_cultureBefore=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			ScratchDb.UseDirectConnection();
			ScratchDb.Connect(_dbName);
			RefreshCaches();
		}

		[TestCleanup]
		public void End() {
			if(_cultureBefore!=null) {
				Thread.CurrentThread.CurrentCulture=_cultureBefore;
				_cultureBefore=null;
			}
			EhrStylesheetStarter.FuncStartChild=null;
			ScratchDb.UseDirectConnection();
			//The tests of the other classes work on the EHR-1 scratch database: back to it, with its own caches, when it exists.
			if(_isBuilt && ScratchDb.SkipReason==null && Exists(ScratchDb.DbName)) {
				ScratchDb.Connect(ScratchDb.DbName);
				RefreshCaches();
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(!_isBuilt || Environment.GetEnvironmentVariable("EHR_KEEP_DB")=="1") {
				return;
			}
			try {
				Drop(_dbName);
			}
			catch(Exception) {
				//The stale clean-up of a later run drops it.
			}
		}

		private static void RefreshCaches() {
			if(!Prefs.DictIsNull()) {
				Prefs.ClearCache();
			}
			Cache.Refresh(InvalidType.Prefs,InvalidType.Providers,InvalidType.Security);
			Userods.RefreshCache();
			Security.CurUser=Userods.GetUser(1);
			Security.PasswordTyped="";
		}

		#region Scratch database
		private static void Build() {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			CultureInfo cultureBefore=Thread.CurrentThread.CurrentCulture;
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			try {
				DropStale();
				_isBuilt=true;//Set before the database exists, so ClassCleanup drops a half-built one too (Drop uses IF EXISTS).
				using(MySqlConnection connection=ScratchDb.OpenServer()) {
					ScratchDb.Exec(connection,"CREATE DATABASE `"+_dbName+"` CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
				}
				string text=File.ReadAllText(TestPaths.Combine(ScratchDb.RepoPathInitSql),new UTF8Encoding(false)).Replace("\r\n","\n");
				using(MySqlConnection connection=ScratchDb.OpenServer(_dbName)) {
					//Every statement of the generated file ends with a semicolon at the end of a line, and no value holds a raw line break.
					foreach(string statement in text.Split(new[] { ";\n" },StringSplitOptions.None)) {
						if(statement.Trim()=="") {
							continue;
						}
						ScratchDb.Exec(connection,statement);
					}
				}
				ScratchDb.UseDirectConnection();
				ScratchDb.Connect(_dbName);
				RefreshCaches();
				ForkMigrationRunner.Run();
				RefreshCaches();
				MakeRows();
			}
			catch(Exception ex) {
				_buildError="Could not build the ehr3_ scratch database: "+ex.GetBaseException().Message;
			}
			finally {
				Thread.CurrentThread.CurrentCulture=cultureBefore;
			}
		}

		///<summary>The practice, a provider with a made-up NPI, one SNOMED CT row with a TEST code and one CVX row (the CCD checks that both tables
		///have rows), a vaccine, and two patients: A with a race and an ethnicity (CDCREC) and a vaccination, B with a non-ASCII name. Database
		///tests run one at a time on one thread (MSTest v1), so the rows are made once, by the first test.</summary>
		private static void MakeRows() {
			Provider provider=new Provider();
			provider.Abbr="TESTPROV";
			provider.LName="Testprovider";
			provider.FName="Avery";
			provider.NationalProvID="1234567893";
			Providers.Insert(provider);
			Prefs.UpdateString(PrefName.PracticeTitle,"Example Test Dental");
			Prefs.UpdateString(PrefName.PracticePhone,"5550100100");
			Prefs.UpdateString(PrefName.PracticeAddress,"100 Example Street");
			Prefs.UpdateString(PrefName.PracticeAddress2,"");
			Prefs.UpdateString(PrefName.PracticeCity,"Exampleton");
			Prefs.UpdateString(PrefName.PracticeST,"ZZ");
			Prefs.UpdateLong(PrefName.PracticeDefaultProv,provider.ProvNum);
			RefreshCaches();
			Snomed snomed=new Snomed();
			snomed.SnomedCode="TEST0001";
			snomed.Description="TEST concept";
			Snomeds.Insert(snomed);
			Cvx cvx=new Cvx();
			cvx.CvxCode="08";
			cvx.Description="TEST vaccine description";
			Cvxs.Insert(cvx);
			VaccineDef vaccineDef=new VaccineDef();
			vaccineDef.CVXCode="08";
			vaccineDef.VaccineName="TEST vaccine";
			VaccineDefs.Insert(vaccineDef);
			Patient patientA=MakePatient("Riley","Testpatient","200 Example Avenue","Exampleton",new DateTime(1980,6,15),PatientGender.Female,provider.ProvNum);
			patientA.HmPhone="5550100200";
			patientA.Language="eng";
			Patients.Insert(patientA,false);
			_patNumA=patientA.PatNum;
			//CDCREC (a US government code system): a race and an ethnicity, with the rows of the cdcrec table the race lookup joins.
			foreach(string[] arrayCdcrec in new[] { new[] { "2106-3","R5","White" },new[] { "2186-5","E2","Not Hispanic or Latino" } }) {
				Cdcrec cdcrec=new Cdcrec();
				cdcrec.CdcrecCode=arrayCdcrec[0];
				cdcrec.HeirarchicalCode=arrayCdcrec[1];
				cdcrec.Description=arrayCdcrec[2];
				Cdcrecs.Insert(cdcrec);
				OpenPmsDentalBusiness.Crud.PatientRaceCrud.Insert(new PatientRace(_patNumA,arrayCdcrec[0]));
			}
			VaccinePat vaccinePat=new VaccinePat();
			vaccinePat.PatNum=_patNumA;
			vaccinePat.VaccineDefNum=vaccineDef.VaccineDefNum;
			vaccinePat.DateTimeStart=new DateTime(2025,10,1,10,0,0);
			vaccinePat.DateTimeEnd=new DateTime(2025,10,1,10,5,0);
			vaccinePat.CompletionStatus=VaccineCompletionStatus.Complete;
			VaccinePats.Insert(vaccinePat);
			Patient patientB=MakePatient("Zoë","Ångström-Núñez","1 Rue de l'Exemple","Søndre Exampleton",new DateTime(1975,2,28),PatientGender.Male,
				provider.ProvNum);
			patientB.WirelessPhone="5550100300";
			Patients.Insert(patientB,false);
			_patNumB=patientB.PatNum;
		}

		private static Patient MakePatient(string fName,string lName,string address,string city,DateTime dateBirth,PatientGender gender,long provNum) {
			Patient patient=new Patient();
			patient.FName=fName;
			patient.LName=lName;
			patient.Address=address;
			patient.City=city;
			patient.State="ZZ";
			patient.Zip="00000";
			patient.Birthdate=dateBirth;
			patient.Gender=gender;
			patient.PriProv=provNum;
			patient.PatStatus=PatientStatus.Patient;
			patient.Position=PatientPosition.Single;
			return patient;
		}

		private static bool Exists(string name) {
			using(MySqlConnection connection=ScratchDb.OpenServer())
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT COUNT(*) FROM information_schema.SCHEMATA WHERE SCHEMA_NAME=@name",connection)) {
				mySqlCommand.Parameters.AddWithValue("@name",name);
				return Convert.ToInt64(mySqlCommand.ExecuteScalar())>0;
			}
		}

		///<summary>Drops this fixture's leftovers of earlier runs (a crash, or EHR_KEEP_DB=1) once they are older than 12 hours.</summary>
		private static void DropStale() {
			Regex regex=new Regex("^"+PREFIX+@"(\d{12})_\d+_\d+_[a-z0-9_]+$");
			List<string> listNames=new List<string>();
			using(MySqlConnection connection=ScratchDb.OpenServer())
			using(MySqlCommand mySqlCommand=new MySqlCommand("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA WHERE SCHEMA_NAME LIKE 'ehr3\\_%'",connection))
			using(MySqlDataReader reader=mySqlCommand.ExecuteReader())
			{
				while(reader.Read()) {
					listNames.Add(reader.GetString(0));
				}
			}
			foreach(string name in listNames) {
				Match match=regex.Match(name);
				if(!match.Success || !DateTime.TryParseExact(match.Groups[1].Value,"yyMMddHHmmss",CultureInfo.InvariantCulture,
					DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out DateTime dateStarted))
				{
					continue;
				}
				if(DateTime.UtcNow-dateStarted<_timeSpanStale) {
					continue;
				}
				try {
					Drop(name);
				}
				catch(Exception) {
					//Another run may be dropping it at the same time.
				}
			}
		}

		private static void Drop(string name) {
			if(!name.StartsWith(PREFIX)) {
				throw new ArgumentException("These tests only drop databases whose name starts with "+PREFIX+".");
			}
			using(MySqlConnection connection=ScratchDb.OpenServer()) {
				ScratchDb.Exec(connection,"DROP DATABASE IF EXISTS `"+name+"`");
			}
		}
		#endregion Scratch database

		#region The documents
		///<summary>The CCD with what changes from one export to the next made fixed: each GUID becomes a numbered one (in order of appearance), each
		///time stamp written from the clock between dateTimeBefore and dateTimeAfter becomes 2026-11-02 09:30:00, and every time zone offset -0500,
		///so neither the day nor the computer's time zone changes the page. And, as in the copy of upstream's CCD-full.xml, every code of a code
		///system other than HL7's own, CDCREC, CVX and HCPCS Level II (the LOINC section codes, the NUCC taxonomy, ...) with its display name
		///becomes a TEST value, so no golden page holds a literal of the engine's codes (docs/specs/ehr-rebuild-plan.md, section 6).</summary>
		public static string MakeStable(string xml,DateTime dateTimeBefore,DateTime dateTimeAfter) {
			Dictionary<string,string> dictCodes=new Dictionary<string,string>();
			xml=Regex.Replace(xml,@"<[A-Za-z][^<>]*\scodeSystem=""([^""]*)""[^<>]*>",match => {
				string codeSystem=match.Groups[1].Value;
				if(codeSystem.StartsWith("2.16.840.1.113883.5.") || codeSystem=="2.16.840.1.113883.6.238" || codeSystem=="2.16.840.1.113883.12.292"
					|| codeSystem=="2.16.840.1.113883.6.285")
				{
					return match.Value;
				}
				string tag=Regex.Replace(match.Value,@"(\scode="")([^""]*)("")",matchCode => {
					string key=codeSystem+"|"+matchCode.Groups[2].Value;
					if(!dictCodes.TryGetValue(key,out string code)) {
						code="TEST"+(dictCodes.Count+1).ToString("D4",CultureInfo.InvariantCulture);
						dictCodes.Add(key,code);
					}
					return matchCode.Groups[1].Value+code+matchCode.Groups[3].Value;
				});
				return Regex.Replace(tag,@"(\sdisplayName="")([^""]*)("")","$1TEST display$3");
			});
			Dictionary<string,string> dictGuids=new Dictionary<string,string>();
			xml=Regex.Replace(xml,@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",match => {
				if(!dictGuids.TryGetValue(match.Value,out string guid)) {
					guid="00000000-0000-0000-0000-"+(dictGuids.Count+1).ToString("D12",CultureInfo.InvariantCulture);
					dictGuids.Add(match.Value,guid);
				}
				return guid;
			});
			return Regex.Replace(xml,@"\b(\d{14})([+-]\d{4})\b",match => {
				if(DateTime.TryParseExact(match.Groups[1].Value,"yyyyMMddHHmmss",CultureInfo.InvariantCulture,DateTimeStyles.None,out DateTime dateTime)
					&& dateTime>=dateTimeBefore.AddMinutes(-1) && dateTime<=dateTimeAfter.AddMinutes(1))
				{
					return "20261102093000-0500";
				}
				return match.Groups[1].Value+"-0500";
			});
		}

		///<summary>A CCD made by generate, made stable, checked for what the patient's page needs, rendered and compared with its golden page.</summary>
		private static string RenderGenerated(Func<string> funcGenerate,string goldenName) {
			DateTime dateTimeBefore=DateTime.Now;
			string xml=funcGenerate();
			DateTime dateTimeAfter=DateTime.Now;
			Assert.IsFalse(string.IsNullOrEmpty(xml));
			StringAssert.Contains(xml,"<?xml-stylesheet type=\"text/xsl\" href=\"ccd.xsl\"?>","The program's CCD points to ccd.xsl beside it.");
			string xmlStable=MakeStable(xml,dateTimeBefore,dateTimeAfter);
			Assert.IsFalse(Regex.IsMatch(xmlStable,@"\b[0-9a-f]{8}-(?!0000-0000-0000-)[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b"),"Every GUID is fixed.");
			string html=CdaGolden.Render(EhrRenderKind.Ccd,xmlStable);
			CdaGolden.AssertMatches(goldenName,html);
			return html;
		}

		[TestMethod]
		public void SummaryOfCare_RendersAsRecorded() {
			Patient patient=Patients.GetPat(_patNumA);
			string html=RenderGenerated(() => {
				string xml=EhrCCD.GenerateSummaryOfCare(patient,out string warnings);
				return xml;
			},"Generated-SummaryOfCare.html");
			string text=CdaGolden.VisibleText(html);
			StringAssert.Contains(text,"Riley TESTPATIENT","CDA.xsl writes the family name in capitals.");
			StringAssert.Contains(text,"Example Test Dental");
			StringAssert.Contains(text,"TEST vaccine description","The immunization row of the narrative.");
		}

		[TestMethod]
		public void ClinicalSummary_RendersAsRecorded() {
			Patient patient=Patients.GetPat(_patNumA);
			string html=RenderGenerated(() => EhrCCD.GenerateClinicalSummary(patient,true,true,true,true,true,true,true,true,true,true,true,true,
				"TEST instructions: rest and drink water.",new DateTime(2025,10,1),out string warnings),"Generated-ClinicalSummary.html");
			StringAssert.Contains(CdaGolden.VisibleText(html),"TEST instructions: rest and drink water.");
		}

		///<summary>The patient with accents in each name and the city: the page keeps them (UTF-8 to the child and back, no code page).</summary>
		[TestMethod]
		public void PatientExport_NonAsciiName_RendersAsRecorded() {
			Patient patient=Patients.GetPat(_patNumB);
			string html=RenderGenerated(() => EhrCCD.GeneratePatientExport(patient,out string warnings),"Generated-PatientExport-NonAscii.html");
			string text=CdaGolden.VisibleText(html);
			StringAssert.Contains(text,"Zoë");
			//CDA.xsl writes the family name in capitals with translate(), which knows a to z only: the other letters stay as they are.
			StringAssert.Contains(text,"ÅNGSTRöM-NúñEZ");
			StringAssert.Contains(text,"Søndre Exampleton");
		}

		///<summary>MakeStable leaves nothing of the clock or the time zone: two documents made at different times compare equal.</summary>
		[TestMethod]
		public void MakeStable_FixesGuidsTimesAndOffsets() {
			DateTime dateTime=new DateTime(2026,11,9,8,15,30);
			string xml="<id root=\"0f8fad5b-d9cb-469f-a165-70867728950e\"/><id root=\"7c9e6679-7425-40de-944b-e07fc1f90ae7\"/>"
				+"<id root=\"0f8fad5b-d9cb-469f-a165-70867728950e\"/><effectiveTime value=\"20261109081530-0600\"/><time value=\"19800615000000+0100\"/>"
				+"<code code=\"TESTX\" codeSystem=\"2.16.840.1.113883.6.101\" displayName=\"A taxonomy\"/><code code=\"F\" codeSystem=\"2.16.840.1.113883.5.1\"/>"
				+"<code code=\"08\" codeSystem=\"2.16.840.1.113883.12.292\" displayName=\"TEST vaccine\"/>";
			Assert.AreEqual("<id root=\"00000000-0000-0000-0000-000000000001\"/><id root=\"00000000-0000-0000-0000-000000000002\"/>"
				+"<id root=\"00000000-0000-0000-0000-000000000001\"/><effectiveTime value=\"20261102093000-0500\"/><time value=\"19800615000000-0500\"/>"
				+"<code code=\"TEST0001\" codeSystem=\"2.16.840.1.113883.6.101\" displayName=\"TEST display\"/><code code=\"F\" codeSystem=\"2.16.840.1.113883.5.1\"/>"
				+"<code code=\"08\" codeSystem=\"2.16.840.1.113883.12.292\" displayName=\"TEST vaccine\"/>",
				MakeStable(xml,dateTime,dateTime));
		}
		#endregion The documents
	}
}
