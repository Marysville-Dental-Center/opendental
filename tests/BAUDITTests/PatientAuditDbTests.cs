// Database tests for package B-AUDIT on a scratch database (see ScratchDb): every save path that changes a patient's preferred name or clinic
// writes a PatientEdit Audit Trail entry, once, and the paths that change other fields do not add these lines. Synthetic rows only. Skipped
// (inconclusive) when the local MariaDB does not answer.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using DataConnectionBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;
using OpenDentBusiness.HL7;

namespace BAUDITTests {
	[TestClass]
	public class PatientAuditDbTests {
		private static readonly string DB_NAME=ScratchDb.NewName();
		private static string _skipReason;
		private static long _clinicNumNorth;
		private static long _clinicNumSouth;
		private static long _clinicNumEast;
		private static long _clinicNumWest;
		private const string PREFERRED_LINE_START="Patient preferred name changed from";
		private const string CLINIC_LINE_START="Patient clinic changed from";

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_skipReason=ScratchDb.GetSkipReason();
			if(_skipReason!=null) {
				return;
			}
			try {
				ScratchDb.DropStale();
				ScratchDb.Create(DB_NAME);
			}
			catch(Exception ex) {
				_skipReason="Could not build the scratch database: "+ex.GetBaseException().Message;
				return;
			}
			_clinicNumNorth=Clinics.Insert(new Clinic { Abbr="NORTH",Description="BAudit North Office" });
			_clinicNumSouth=Clinics.Insert(new Clinic { Abbr="SOUTH",Description="BAudit South Office" });
			_clinicNumEast=Clinics.Insert(new Clinic { Abbr="EAST",Description="BAudit East Office" });
			_clinicNumWest=Clinics.Insert(new Clinic { Abbr="WEST",Description="BAudit West Office" });
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			if(_skipReason!=null || Environment.GetEnvironmentVariable("BAUDIT_TEST_KEEP_DB")=="1") {
				return;
			}
			try {
				ScratchDb.Drop(DB_NAME);
			}
			catch(Exception) {
				//Harmless to leave behind; the name is unique to this run and a later run drops it once it is stale.
			}
		}

		[TestInitialize]
		public void TestInitialize() {
			if(_skipReason!=null) {
				Assert.Inconclusive(_skipReason);
			}
			ScratchDb.Connect(DB_NAME);
			Security.CurUser=new Userod { UserNum=1,UserName="baudittest" };
			Thread.CurrentThread.CurrentCulture=new CultureInfo("en-US");
			Clinics.RefreshCache();
		}

		#region Helpers
		private static int _patCounter;
		///<summary>Per synthetic patient, the last securitylog row written while AddPatient set the patient up. GetPatientEditLogs only returns rows
		///after it, so a test sees exactly the entries of the action it tests. Setting up writes its own PatientEdit row since M2-AUDIT (the
		///birthdate the patient is created with is audited as added).</summary>
		private static readonly Dictionary<long,long> _dictSetupLogNums=new Dictionary<long,long>();

		///<summary>A new synthetic patient who is his own guarantor unless one is given.</summary>
		private static Patient AddPatient(string preferred,long clinicNum,long guarantor=0) {
			_patCounter++;
			Patient patient=new Patient {
				LName="Baudit"+_patCounter.ToString("000"),
				FName="Synthetic",
				Preferred=preferred,
				ClinicNum=clinicNum,
				PatStatus=PatientStatus.Patient,
				Birthdate=new DateTime(1980,1,1),
				Address="1 Synthetic Way",
				City="Testville",
				State="WA",
				Zip="98000",
			};
			Patients.Insert(patient,false);
			Patient patientOld=patient.Copy();
			patient.Guarantor=(guarantor==0 ? patient.PatNum : guarantor);
			Patients.Update(patient,patientOld);
			_dictSetupLogNums[patient.PatNum]=PIn.Long(DataCore.GetScalar("SELECT COALESCE(MAX(SecurityLogNum),0) FROM securitylog"));
			return Patients.GetPat(patient.PatNum);
		}

		///<summary>The patient's PatientEdit entries written after AddPatient set the patient up.</summary>
		private static List<SecurityLog> GetPatientEditLogs(long patNum) {
			_dictSetupLogNums.TryGetValue(patNum,out long setupLogNum);
			return OpenDentBusiness.Crud.SecurityLogCrud.SelectMany("SELECT * FROM securitylog WHERE PermType="+POut.Int((int)EnumPermType.PatientEdit)
				+" AND PatNum="+POut.Long(patNum)+" AND SecurityLogNum>"+POut.Long(setupLogNum)+" ORDER BY SecurityLogNum");
		}

		private static string AllText(List<SecurityLog> listSecurityLogs) {
			return string.Join("\r\n",listSecurityLogs.Select(x => x.LogText));
		}

		private static int CountOccurrences(string text,string value) {
			int count=0;
			int index=text.IndexOf(value,StringComparison.Ordinal);
			while(index>=0) {
				count++;
				index=text.IndexOf(value,index+value.Length,StringComparison.Ordinal);
			}
			return count;
		}

		///<summary>Saves the patient the way the Edit Patient Information window does (FormPatientEdit.butOK_Click).</summary>
		private static void SaveAsPatientEditWindow(Patient patient,Patient patientOld,bool addressSame=false) {
			Family family=Patients.GetFamily(patient.PatNum);
			PatientNote patientNote=PatientNotes.Refresh(patient.PatNum,patient.Guarantor);
			EhrPatient ehrPatient=EhrPatients.Refresh(patient.PatNum);
			List<PatientRace> listPatientRaces=PatientRaces.GetForPatient(patient.PatNum);
			CommOptOut commOptOut=CommOptOuts.Refresh(patient.PatNum);
			Patients.SavePatientEdit(Security.CurUser,patient,patientOld,patientNote,ehrPatient,family,false,false,false,addressSame,false,false,false,false,
				null,0,listPatientRaces,commOptOut);
		}
		#endregion Helpers

		#region InsertAddressChangeSecurityLogEntry
		[TestMethod]
		public void AddressChangeEntry_PreferredNameChange_IsLogged() {
			Patient patientOld=AddPatient("Al",0);
			Patient patient=patientOld.Copy();
			patient.Preferred="Lex";
			Patients.InsertAddressChangeSecurityLogEntry(patientOld,patient);
			List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Patient preferred name changed from 'Al' to 'Lex'");
		}

		[TestMethod]
		public void AddressChangeEntry_ClinicChange_NamesBothClinics() {
			Patient patientOld=AddPatient("",_clinicNumNorth);
			Patient patient=patientOld.Copy();
			patient.ClinicNum=_clinicNumSouth;
			Patients.InsertAddressChangeSecurityLogEntry(patientOld,patient);
			Patient patientOld2=patient.Copy();
			patient.ClinicNum=0;
			Patients.InsertAddressChangeSecurityLogEntry(patientOld2,patient);
			List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
			Assert.AreEqual(2,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Patient clinic changed from 'NORTH' to 'SOUTH'");
			StringAssert.Contains(listLogs[1].LogText,"Patient clinic changed from 'SOUTH' to 'None'");
		}

		[TestMethod]
		public void AddressChangeEntry_NameAndPreferredAndClinic_OneEntry() {
			Patient patientOld=AddPatient("Al",0);
			Patient patient=patientOld.Copy();
			patient.LName="Bauditnew";
			patient.Preferred="Lex";
			patient.ClinicNum=_clinicNumEast;
			Patients.InsertAddressChangeSecurityLogEntry(patientOld,patient);
			List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
			Assert.AreEqual(1,listLogs.Count,"The new lines join the existing PatientEdit entry instead of writing their own.");
			StringAssert.Contains(listLogs[0].LogText,"Patient last name changed from '"+patientOld.LName+"' to 'Bauditnew'");
			StringAssert.Contains(listLogs[0].LogText,"Patient preferred name changed from 'Al' to 'Lex'");
			StringAssert.Contains(listLogs[0].LogText,"Patient clinic changed from 'None' to 'EAST'");
		}

		[TestMethod]
		public void AddressChangeEntry_OtherFieldsOnly_NoPreferredOrClinicLines() {
			Patient patientOld=AddPatient("Al",_clinicNumNorth);
			Patient patient=patientOld.Copy();
			patient.City="Othertown";
			Patients.InsertAddressChangeSecurityLogEntry(patientOld,patient);
			string text=AllText(GetPatientEditLogs(patient.PatNum));
			StringAssert.Contains(text,"Patient city changed");
			Assert.IsFalse(text.Contains(PREFERRED_LINE_START));
			Assert.IsFalse(text.Contains(CLINIC_LINE_START));
		}

		[TestMethod]
		public void AddressChangeEntry_NothingChanged_NoEntry() {
			Patient patientOld=AddPatient("Al",_clinicNumNorth);
			Patients.InsertAddressChangeSecurityLogEntry(patientOld,patientOld.Copy());
			Assert.AreEqual(0,GetPatientEditLogs(patientOld.PatNum).Count);
		}
		#endregion InsertAddressChangeSecurityLogEntry

		#region Edit Patient Information window (Patients.SavePatientEdit)
		[TestMethod]
		public void SavePatientEdit_PreferredAndClinic_LoggedOnceAndSaved() {
			Patient patientOld=AddPatient("Al",_clinicNumNorth);
			Patient patient=patientOld.Copy();
			patient.Preferred="Lex";
			patient.ClinicNum=_clinicNumSouth;
			SaveAsPatientEditWindow(patient,patientOld);
			Patient patientDb=Patients.GetPat(patient.PatNum);
			Assert.AreEqual("Lex",patientDb.Preferred);
			Assert.AreEqual(_clinicNumSouth,patientDb.ClinicNum);
			string text=AllText(GetPatientEditLogs(patient.PatNum));
			Assert.AreEqual(1,CountOccurrences(text,"Patient preferred name changed from 'Al' to 'Lex'"),text);
			Assert.AreEqual(1,CountOccurrences(text,"Patient clinic changed from 'NORTH' to 'SOUTH'"),text);
		}

		[TestMethod]
		public void SavePatientEdit_AddressSameForFamily_FamilyGetsNoPreferredOrClinicLines() {
			Patient guarantorOld=AddPatient("Al",_clinicNumNorth);
			Patient child=AddPatient("Kid",_clinicNumWest,guarantorOld.PatNum);
			Patient guarantor=guarantorOld.Copy();
			guarantor.Address="2 Synthetic Way";
			guarantor.Preferred="Lex";
			SaveAsPatientEditWindow(guarantor,guarantorOld,addressSame:true);
			string textChild=AllText(GetPatientEditLogs(child.PatNum));
			StringAssert.Contains(textChild,"Patient address changed from '1 Synthetic Way' to '2 Synthetic Way'");
			Assert.IsFalse(textChild.Contains(PREFERRED_LINE_START),textChild);
			Assert.IsFalse(textChild.Contains(CLINIC_LINE_START),textChild);
			string textGuarantor=AllText(GetPatientEditLogs(guarantor.PatNum));
			Assert.AreEqual(1,CountOccurrences(textGuarantor,"Patient preferred name changed from 'Al' to 'Lex'"),textGuarantor);
			Assert.IsFalse(textGuarantor.Contains(CLINIC_LINE_START),textGuarantor);
		}

		[TestMethod]
		public void SavePatientEdit_NewPatient_NoChangeEntry() {
			Patient patientOld=AddPatient("",0);
			Patient patient=patientOld.Copy();
			patient.Preferred="Lex";
			patient.ClinicNum=_clinicNumNorth;
			Family family=Patients.GetFamily(patient.PatNum);
			Patients.SavePatientEdit(Security.CurUser,patient,patientOld,PatientNotes.Refresh(patient.PatNum,patient.Guarantor),
				EhrPatients.Refresh(patient.PatNum),family,true,false,false,false,false,false,false,false,null,0,PatientRaces.GetForPatient(patient.PatNum),
				CommOptOuts.Refresh(patient.PatNum));
			Assert.AreEqual(0,GetPatientEditLogs(patient.PatNum).Count,"A new patient's first save is logged as a creation, not as changes.");
		}
		#endregion Edit Patient Information window (Patients.SavePatientEdit)

		#region Moving all patients of a clinic (Clinics window, Patients.ChangeClinicsForAll)
		[TestMethod]
		public void ChangeClinicsForAll_LogsEachMovedPatientOnly() {
			long clinicNumFrom=Clinics.Insert(new Clinic { Abbr="MOVEA",Description="BAudit Move From" });
			long clinicNumTo=Clinics.Insert(new Clinic { Abbr="MOVEB",Description="BAudit Move To" });
			Clinics.RefreshCache();
			List<Patient> listMoved=new List<Patient> { AddPatient("",clinicNumFrom),AddPatient("",clinicNumFrom),AddPatient("",clinicNumFrom) };
			Patient patientStays=AddPatient("",clinicNumTo);
			Patient patientElsewhere=AddPatient("",_clinicNumNorth);
			Patients.ChangeClinicsForAll(clinicNumFrom,clinicNumTo);
			foreach(Patient patient in listMoved) {
				Assert.AreEqual(clinicNumTo,Patients.GetPat(patient.PatNum).ClinicNum);
				List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
				Assert.AreEqual(1,listLogs.Count);
				StringAssert.Contains(listLogs[0].LogText,"Patient clinic changed from 'MOVEA' to 'MOVEB'");
				StringAssert.Contains(listLogs[0].LogText,"From moving all patients of the clinic to another clinic.");
			}
			Assert.AreEqual(0,GetPatientEditLogs(patientStays.PatNum).Count);
			Assert.AreEqual(0,GetPatientEditLogs(patientElsewhere.PatNum).Count);
		}

		[TestMethod]
		public void ChangeClinicsForAll_LargeClinic_EveryPatientLoggedAndHashed() {
			long clinicNumFrom=Clinics.Insert(new Clinic { Abbr="BULKA",Description="BAudit Bulk From" });
			long clinicNumTo=Clinics.Insert(new Clinic { Abbr="BULKB",Description="BAudit Bulk To" });
			Clinics.RefreshCache();
			const int countPatients=1201;//More than two batches of 500
			StringBuilder stringBuilder=new StringBuilder("INSERT INTO patient (LName,FName,ClinicNum,PatStatus) VALUES ");
			for(int i=0;i<countPatients;i++) {
				if(i>0) {
					stringBuilder.Append(",");
				}
				stringBuilder.Append("('Bauditbulk"+i+"','Synthetic',"+POut.Long(clinicNumFrom)+","+POut.Int((int)PatientStatus.Patient)+")");
			}
			DataCore.NonQ(stringBuilder.ToString());
			Patients.ChangeClinicsForAll(clinicNumFrom,clinicNumTo);
			string whereLogs="WHERE securitylog.PermType="+POut.Int((int)EnumPermType.PatientEdit)
				+" AND securitylog.LogText LIKE '%BULKA%' AND securitylog.PatNum IN (SELECT PatNum FROM patient WHERE LName LIKE 'Bauditbulk%')";
			Assert.AreEqual(countPatients.ToString(),DataCore.GetScalar("SELECT COUNT(DISTINCT securitylog.PatNum) FROM securitylog "+whereLogs));
			Assert.AreEqual(countPatients.ToString(),DataCore.GetScalar("SELECT COUNT(*) FROM securitylog "+whereLogs));
			Assert.AreEqual(countPatients.ToString(),DataCore.GetScalar("SELECT COUNT(*) FROM securitylog INNER JOIN securityloghash "
				+"ON securityloghash.SecurityLogNum=securitylog.SecurityLogNum "+whereLogs),"Every entry gets its tamper hash.");
		}

		[TestMethod]
		public void ChangeClinicsForAll_SameClinic_NoEntries() {
			Patient patient=AddPatient("",_clinicNumWest);
			Patients.ChangeClinicsForAll(_clinicNumWest,_clinicNumWest);
			Assert.AreEqual(0,GetPatientEditLogs(patient.PatNum).Count);
		}
		#endregion Moving all patients of a clinic (Clinics window, Patients.ChangeClinicsForAll)

		#region Other save paths
		[TestMethod]
		public void PreferredAndClinicEntry_AddsSourceLine() {
			Patient patientOld=AddPatient("Al",0);
			Patient patient=patientOld.Copy();
			patient.Preferred="Lex";
			Patients.Update(patient,patientOld);
			Patients.InsertPreferredAndClinicChangeSecurityLogEntry(patientOld,patient,"sheet import");
			List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
			Assert.AreEqual(1,listLogs.Count);
			Assert.AreEqual("Patient preferred name changed from 'Al' to 'Lex'\r\nFrom sheet import.",listLogs[0].LogText);
		}

		[TestMethod]
		public void PreferredAndClinicEntry_OtherFieldsOnly_NoEntry() {
			Patient patientOld=AddPatient("Al",_clinicNumNorth);
			Patient patient=patientOld.Copy();
			patient.LName="Bauditother";
			patient.City="Othertown";
			Patients.InsertPreferredAndClinicChangeSecurityLogEntry(patientOld,patient,"eForm import");
			Assert.AreEqual(0,GetPatientEditLogs(patient.PatNum).Count);
		}

		[TestMethod]
		public void CloneSynch_PreferredNameCopied_LoggedOnClone() {
			Patient patientOriginal=AddPatient("Lex",_clinicNumNorth);
			Patient patientClone=AddPatient("",_clinicNumNorth);
			Patients.SynchCloneWithPatient(patientOriginal,patientClone);
			Assert.AreEqual("LEX",Patients.GetPat(patientClone.PatNum).Preferred);
			List<SecurityLog> listLogs=GetPatientEditLogs(patientClone.PatNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Patient preferred name changed from '' to 'LEX'");
			StringAssert.Contains(listLogs[0].LogText,"From clone synch with patient #"+patientOriginal.PatNum+".");
			Assert.AreEqual(0,GetPatientEditLogs(patientOriginal.PatNum).Count);
		}

		[TestMethod]
		public void InboundHl7Pv1_ClinicChange_Logged() {
			Patient patient=AddPatient("",_clinicNumNorth);
			HL7DefSegment hl7DefSegment=new HL7DefSegment { SegmentName=SegmentNameHL7.PV1 };
			hl7DefSegment.hl7DefFields=new List<HL7DefField> { new HL7DefField { FieldName="pat.location",OrdinalPos=3 } };
			SegmentHL7 segmentHL7=new SegmentHL7("PV1|1|O|BAudit South Office^^^^^c");
			MessageParser.ProcessPV1(patient,null,hl7DefSegment,segmentHL7);
			Assert.AreEqual(_clinicNumSouth,Patients.GetPat(patient.PatNum).ClinicNum);
			List<SecurityLog> listLogs=GetPatientEditLogs(patient.PatNum);
			Assert.AreEqual(1,listLogs.Count);
			StringAssert.Contains(listLogs[0].LogText,"Patient clinic changed from 'NORTH' to 'SOUTH'");
			StringAssert.Contains(listLogs[0].LogText,"From an inbound HL7 message (PV1 segment).");
		}

		[TestMethod]
		public void InboundHl7Pv1_UnknownClinic_NoEntry() {
			Patient patient=AddPatient("",_clinicNumNorth);
			HL7DefSegment hl7DefSegment=new HL7DefSegment { SegmentName=SegmentNameHL7.PV1 };
			hl7DefSegment.hl7DefFields=new List<HL7DefField> { new HL7DefField { FieldName="pat.location",OrdinalPos=3 } };
			MessageParser.ProcessPV1(patient,null,hl7DefSegment,new SegmentHL7("PV1|1|O|No Such Office^^^^^c"));
			Assert.AreEqual(_clinicNumNorth,Patients.GetPat(patient.PatNum).ClinicNum);
			Assert.AreEqual(0,GetPatientEditLogs(patient.PatNum).Count);
		}

		[TestMethod]
		public void EmailPhoneSameForFamily_NoPreferredOrClinicLines() {
			Patient guarantor=AddPatient("Al",_clinicNumNorth);
			Patient child=AddPatient("Kid",_clinicNumSouth,guarantor.PatNum);
			guarantor.Email="synthetic@example.invalid";
			Patients.UpdateEmailPhoneForFam(guarantor,true);
			string textChild=AllText(GetPatientEditLogs(child.PatNum));
			StringAssert.Contains(textChild,"Patient Email changed");
			Assert.IsFalse(textChild.Contains(PREFERRED_LINE_START),textChild);
			Assert.IsFalse(textChild.Contains(CLINIC_LINE_START),textChild);
		}
		#endregion Other save paths
	}
}
