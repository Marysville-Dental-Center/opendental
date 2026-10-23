// Tests for hidden medications (v25_1-misc-medication-hide, package A-MED): hidden medications are left out of pickers, but patients who already
// have them still see them. Rules that need no database run anywhere; the rest use a scratch MariaDB database at 127.0.0.1:3306 (root, blank
// password) filled with synthetic rows only, and are skipped (inconclusive) when no server answers or MEDICATION_TEST_DB=skip.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using Newtonsoft.Json;
using OpenDental;
using OpenDentBusiness;

namespace MedicationTests {
	///<summary>Rules that need no database.</summary>
	[TestClass]
	public class HiddenMedicationRuleTests {
		private static Medication Med(long medicationNum,string medName,bool isHidden,long genericNum=0) {
			Medication medication=new Medication();
			medication.MedicationNum=medicationNum;
			medication.MedName=medName;
			medication.GenericNum=(genericNum==0 ? medicationNum : genericNum);
			medication.Notes="";
			medication.IsHidden=isHidden;
			return medication;
		}

		[TestMethod]
		public void FilterHidden_LeavesOutHiddenUnlessAsked() {
			List<Medication> listMedications=new List<Medication> { Med(1,"Amoxicillin",false),Med(2,"Penicillin VK",true),Med(3,"Ibuprofen",false) };
			List<Medication> listVisible=Medications.FilterHidden(listMedications,includeHidden:false);
			CollectionAssert.AreEqual(new long[] { 1,3 },listVisible.Select(x => x.MedicationNum).ToArray());
			List<Medication> listAll=Medications.FilterHidden(listMedications,includeHidden:true);
			CollectionAssert.AreEqual(new long[] { 1,2,3 },listAll.Select(x => x.MedicationNum).ToArray());
			Assert.AreEqual(3,listMedications.Count,"The passed in list is not changed.");
			Assert.AreEqual(0,Medications.FilterHidden(null,false).Count);
		}

		[TestMethod]
		public void IsSearchMatch_IgnoresCaseAndBlankMatchesAll() {
			Medication medication=Med(1,"Amoxicillin",false);
			Assert.IsTrue(Medications.IsSearchMatch(medication,""));
			Assert.IsTrue(Medications.IsSearchMatch(medication,null));
			Assert.IsTrue(Medications.IsSearchMatch(medication,"MOXI"));
			Assert.IsFalse(Medications.IsSearchMatch(medication,"penicillin"));
			Assert.IsFalse(Medications.IsSearchMatch(Med(2,null,false),"a"));
		}

		[TestMethod]
		public void GetHiddenChangedLogText_OnlyWhenTheFlagChanges() {
			Medication medicationOld=Med(1,"Amoxicillin",false);
			Medication medication=medicationOld.Copy();
			Assert.AreEqual("",Medications.GetHiddenChangedLogText(medication,medicationOld));
			medication.IsHidden=true;
			string textHidden=Medications.GetHiddenChangedLogText(medication,medicationOld);
			StringAssert.Contains(textHidden,"Amoxicillin");
			StringAssert.Contains(textHidden,"hidden");
			Assert.IsFalse(textHidden.Contains("unhidden"));
			StringAssert.Contains(Medications.GetHiddenChangedLogText(medicationOld,medication),"unhidden");
			Assert.AreEqual("",Medications.GetHiddenChangedLogText(null,medicationOld));
		}

		[TestMethod]
		public void AreMedicationsEqual_SeesTheHiddenFlag() {
			Medication medicationOld=Med(1,"Amoxicillin",false);
			Medication medication=medicationOld.Copy();
			Assert.IsTrue(Medications.AreMedicationsEqual(medication,medicationOld));
			medication.IsHidden=true;
			Assert.IsFalse(Medications.AreMedicationsEqual(medication,medicationOld));
		}

		[TestMethod]
		public void Crud_HasIsHiddenColumnAfterRxCui() {
			//25.3.48: medication.IsHidden tinyint(4) NOT NULL, right after RxCui.
			System.Data.DataTable table=OpenDentBusiness.Crud.MedicationCrud.ListToTable(new List<Medication> { Med(5,"Ibuprofen",true) });
			int indexRxCui=table.Columns.IndexOf("RxCui");
			Assert.IsTrue(indexRxCui>=0);
			Assert.AreEqual(indexRxCui+1,table.Columns.IndexOf("IsHidden"));
			List<Medication> listMedications=OpenDentBusiness.Crud.MedicationCrud.TableToList(table);
			Assert.IsTrue(listMedications[0].IsHidden);
		}

		[TestMethod]
		public void Import_LeavesTheHiddenFlagOff() {
			//An export from an office where the medication was hidden.  Import never reads the flag: imported medications start visible.
			string json="[{\"MedName\":\"Amoxicillin\",\"GenericName\":\"Amoxicillin\",\"Notes\":\"\",\"RxCui\":0,\"IsHidden\":true},"
				+"{\"MedName\":\"Ibuprofen\",\"GenericName\":\"Ibuprofen\",\"Notes\":\"\",\"RxCui\":0,\"IsHidden\":false}]";
			string fileName=Path.Combine(Path.GetTempPath(),"amed_import_"+Guid.NewGuid().ToString("N")+".txt");
			try {
				File.WriteAllText(fileName,json);
				List<Medication> listMedications=MedicationL.GetMedicationsFromFile(fileName);
				Assert.AreEqual(2,listMedications.Count);
				Assert.IsTrue(listMedications.All(x => !x.IsHidden));
			}
			finally {
				File.Delete(fileName);
			}
		}
	}

	///<summary>Tests against a scratch database with synthetic medications and one synthetic patient medication.</summary>
	[TestClass]
	public class HiddenMedicationDbTests {
		private const string CONNECTION="Server=127.0.0.1;Port=3306;User ID=root;Password=;Connection Timeout=3";
		///<summary>Unique per run, so runs from other worktrees on the same server never share or drop each other's database.</summary>
		private static readonly string _dbName="amed_"+DateTime.UtcNow.ToString("yyMMddHHmmss")+"_"+System.Diagnostics.Process.GetCurrentProcess().Id;
		private static string _skipReason;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			if(Environment.GetEnvironmentVariable("MEDICATION_TEST_DB")=="skip") {
				_skipReason="MEDICATION_TEST_DB=skip";
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+_dbName);
					Exec(connection,"CREATE DATABASE "+_dbName+" CHARACTER SET utf8mb3 COLLATE utf8mb3_general_ci");
					Exec(connection,"USE "+_dbName);
					//The definitions in Open Dental 25.3.48.
					Exec(connection,"CREATE TABLE medication (MedicationNum bigint(20) NOT NULL AUTO_INCREMENT,MedName varchar(255) DEFAULT '',"
						+"GenericNum bigint(20) NOT NULL,Notes text DEFAULT NULL,"
						+"DateTStamp timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),RxCui bigint(20) NOT NULL,"
						+"IsHidden tinyint(4) NOT NULL,PRIMARY KEY (MedicationNum),KEY RxCui (RxCui)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"CREATE TABLE medicationpat (MedicationPatNum bigint(20) NOT NULL AUTO_INCREMENT,PatNum bigint(20) NOT NULL,"
						+"MedicationNum bigint(20) NOT NULL,PatNote text DEFAULT NULL,"
						+"DateTStamp timestamp NOT NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),"
						+"DateStart date NOT NULL DEFAULT '0001-01-01',DateStop date NOT NULL DEFAULT '0001-01-01',ProvNum bigint(20) NOT NULL,"
						+"MedDescript varchar(255) NOT NULL,RxCui bigint(20) NOT NULL,ErxGuid varchar(255) NOT NULL,IsCpoe tinyint(4) NOT NULL,"
						+"PRIMARY KEY (MedicationPatNum),KEY ProvNum (ProvNum),KEY PatNum (PatNum),KEY RxCui (RxCui)) ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					//The CRUD layer reads RandomPrimaryKeys before every insert.
					Exec(connection,"CREATE TABLE preference (PrefName varchar(255) NOT NULL DEFAULT '',ValueString text NOT NULL,"
						+"PrefNum bigint(20) NOT NULL AUTO_INCREMENT,Comments text DEFAULT NULL,PRIMARY KEY (PrefNum),KEY PrefName (PrefName)) "
						+"ENGINE=MyISAM DEFAULT CHARSET=utf8mb3");
					Exec(connection,"INSERT INTO preference (PrefName,ValueString) VALUES('RandomPrimaryKeys','0')");
				}
				new DataConnection().SetDb("127.0.0.1",_dbName,"root","","root","",DatabaseType.MySql,true);
			}
			catch(Exception ex) {
				_skipReason="No scratch MariaDB database: "+ex.GetBaseException().Message;
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null) {
				return;
			}
			try {
				using(MySqlConnection connection=new MySqlConnection(CONNECTION)) {
					connection.Open();
					Exec(connection,"DROP DATABASE IF EXISTS "+_dbName);
				}
			}
			catch(Exception) {
				//A scratch database left behind holds synthetic rows only.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			DataCore.NonQ("DELETE FROM medicationpat");
			DataCore.NonQ("DELETE FROM medication");
		}

		private static void Exec(MySqlConnection connection,string command) {
			using(MySqlCommand mySqlCommand=new MySqlCommand(command,connection)) {
				mySqlCommand.ExecuteNonQuery();
			}
		}

		///<summary>Inserts a synthetic medication.  A generic when genericNum is 0, otherwise a brand of that generic.</summary>
		private static Medication InsertMed(string medName,bool isHidden,long genericNum=0) {
			Medication medication=new Medication();
			medication.MedName=medName;
			medication.Notes="";
			medication.IsHidden=isHidden;
			medication.GenericNum=genericNum;
			Medications.Insert(medication);
			if(genericNum==0) {
				medication.GenericNum=medication.MedicationNum;
				Medications.Update(medication);
			}
			return medication;
		}

		[TestMethod]
		public void Crud_RoundTripsTheHiddenFlag() {
			Medication medication=InsertMed("Amoxicillin",isHidden:true);
			Assert.IsTrue(Medications.GetMedicationFromDb(medication.MedicationNum).IsHidden);
			Medication medicationOld=medication.Copy();
			medication.IsHidden=false;
			Assert.IsTrue(Medications.Update(medication,medicationOld));
			Assert.IsFalse(Medications.GetMedicationFromDb(medication.MedicationNum).IsHidden);
			Assert.AreEqual("0",DataCore.GetScalar("SELECT IsHidden FROM medication WHERE MedicationNum="+medication.MedicationNum));
		}

		[TestMethod]
		public void Pickers_LeaveOutHidden_MasterListAndMergeSeeAll() {
			Medication medicationVisible=InsertMed("Amoxicillin",isHidden:false);
			Medication medicationHidden=InsertMed("Ampicillin",isHidden:true);
			InsertMed("Ibuprofen",isHidden:false);
			Medications.RefreshCache();
			//Patient, allergy, EHR rule, eForm and reconcile pickers.
			List<long> listPicker=Medications.GetListForPicker("amp").Select(x => x.MedicationNum).ToList();
			Assert.AreEqual(0,listPicker.Count,"The only match for 'amp' is hidden.");
			listPicker=Medications.GetListForPicker().Select(x => x.MedicationNum).ToList();
			Assert.AreEqual(2,listPicker.Count);
			Assert.IsFalse(listPicker.Contains(medicationHidden.MedicationNum));
			Assert.IsTrue(listPicker.Contains(medicationVisible.MedicationNum));
			//Master list with Show Hidden, and the merge tool.
			List<long> listWithHidden=Medications.GetListForPicker("amp",includeHidden:true).Select(x => x.MedicationNum).ToList();
			CollectionAssert.AreEqual(new[] { medicationHidden.MedicationNum },listWithHidden);
			//Export, import duplicate checks and lookups by name still see every row.
			Assert.AreEqual(3,Medications.GetList().Count);
			Assert.IsTrue(Medications.IsMedicationHidden(medicationHidden.MedicationNum));
			Assert.IsFalse(Medications.IsMedicationHidden(medicationVisible.MedicationNum));
			Assert.IsFalse(Medications.IsMedicationHidden(0));
		}

		[TestMethod]
		public void PatientsWhoHaveAHiddenMedication_StillSeeIt() {
			Medication medicationGeneric=InsertMed("Amoxicillin",isHidden:false);
			Medication medicationBrand=InsertMed("Amoxil",isHidden:true,genericNum:medicationGeneric.MedicationNum);
			MedicationPat medicationPat=new MedicationPat();
			medicationPat.PatNum=4242;//synthetic patient
			medicationPat.MedicationNum=medicationBrand.MedicationNum;
			medicationPat.MedDescript="";
			medicationPat.ErxGuid="";
			MedicationPats.Insert(medicationPat);
			Medications.RefreshCache();
			Assert.IsFalse(Medications.GetListForPicker().Any(x => x.MedicationNum==medicationBrand.MedicationNum));
			//The patient's medication list (FormMedical) reads medicationpat and names the medication from the cache.
			List<MedicationPat> listMedicationPats=MedicationPats.Refresh(4242,includeDiscontinued:false);
			Assert.AreEqual(1,listMedicationPats.Count);
			Assert.AreEqual(medicationBrand.MedicationNum,listMedicationPats[0].MedicationNum);
			Assert.IsNotNull(Medications.GetMedication(medicationBrand.MedicationNum));
			Assert.AreEqual("Amoxil(Amoxicillin)",Medications.GetDescription(medicationBrand.MedicationNum));
			Assert.AreEqual("Amoxil",Medications.GetNameOnly(medicationBrand.MedicationNum));
			CollectionAssert.AreEqual(new[] { medicationBrand.MedicationNum },
				Medications.GetMedicationsByPat(4242).Select(x => x.MedicationNum).ToArray());
		}

		[TestMethod]
		public void GetVisibleBrands_ListsOnlyBrandsThatStayVisible() {
			Medication medicationGeneric=InsertMed("Ibuprofen",isHidden:false);
			InsertMed("Motrin",isHidden:false,genericNum:medicationGeneric.MedicationNum);
			InsertMed("Advil",isHidden:true,genericNum:medicationGeneric.MedicationNum);
			CollectionAssert.AreEqual(new[] { "Motrin" },Medications.GetVisibleBrands(medicationGeneric.MedicationNum));
			//Hiding a generic does not hide its brands.
			Medication medicationOld=Medications.GetMedicationFromDb(medicationGeneric.MedicationNum);
			Medication medication=medicationOld.Copy();
			medication.IsHidden=true;
			Medications.Update(medication,medicationOld);
			Medications.RefreshCache();
			List<string> listPicker=Medications.GetListForPicker().Select(x => x.MedName).ToList();
			CollectionAssert.AreEqual(new[] { "Motrin" },listPicker);
			//A brand has no brands.
			Medication medicationBrand=Medications.GetMedicationFromDbByName("Motrin");
			Assert.AreEqual(0,Medications.GetVisibleBrands(medicationBrand.MedicationNum).Count);
		}

		[TestMethod]
		public void Export_IncludesTheHiddenFlag() {
			Medication medication=InsertMed("Amoxicillin",isHidden:true);
			Medications.RefreshCache();
			//MedicationL.ExportMedications writes this private shape as JSON (and also writes an audit row, which needs a signed-in user).
			Type typeExport=typeof(MedicationL).GetNestedType("MedicationExport",BindingFlags.NonPublic);
			Assert.IsNotNull(typeExport);
			object export=Activator.CreateInstance(typeExport,new object[] { medication });
			string json=JsonConvert.SerializeObject(export);
			StringAssert.Contains(json,"\"IsHidden\":true");
			StringAssert.Contains(json,"\"MedName\":\"Amoxicillin\"");
		}
	}
}
