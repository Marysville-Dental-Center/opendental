// Tests without a database for package B-AUDIT: the audit lines for preferred-name and clinic changes. The clinic cache is filled from a
// made-up table, so no database is needed.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace BAUDITTests {
	[TestClass]
	public class AuditTextTests {
		private const long CLINIC_NORTH=901;
		private const long CLINIC_SOUTH=902;
		private const long CLINIC_TWIN=903;//Same abbreviation as CLINIC_NORTH
		private const long CLINIC_BLANK=904;//No abbreviation
		private const long CLINIC_MISSING=999;//Not in the cache, as for a clinic that was deleted

		[TestInitialize]
		public void TestInitialize() {
			List<Clinic> listClinics=new List<Clinic> {
				new Clinic { ClinicNum=CLINIC_NORTH,Abbr="NORTH",Description="North Office" },
				new Clinic { ClinicNum=CLINIC_SOUTH,Abbr="SOUTH",Description="South Office" },
				new Clinic { ClinicNum=CLINIC_TWIN,Abbr="NORTH",Description="North Annex" },
				new Clinic { ClinicNum=CLINIC_BLANK,Abbr="",Description="No Abbreviation" },
			};
			Clinics.FillCacheFromTable(OpenPmsDentalBusiness.Crud.ClinicCrud.ListToTable(listClinics));
		}

		private static Patient Pat(string preferred,long clinicNum) {
			return new Patient { PatNum=5,LName="Testpatient",FName="Alex",Preferred=preferred,ClinicNum=clinicNum };
		}

		[TestMethod]
		public void PreferredName_Changed_GivesOneLine() {
			Assert.AreEqual("Patient preferred name changed from 'Al' to 'Lex'\r\n",
				Patients.GetPreferredAndClinicChangeLogText(Pat("Al",CLINIC_NORTH),Pat("Lex",CLINIC_NORTH)));
		}

		[TestMethod]
		public void PreferredName_AddedOrCleared_ShowsTheBlankSide() {
			Assert.AreEqual("Patient preferred name changed from '' to 'Lex'\r\n",Patients.GetPreferredAndClinicChangeLogText(Pat("",0),Pat("Lex",0)));
			Assert.AreEqual("Patient preferred name changed from 'Lex' to ''\r\n",Patients.GetPreferredAndClinicChangeLogText(Pat("Lex",0),Pat("",0)));
		}

		[TestMethod]
		public void PreferredName_NullAndBlank_AreTheSame() {
			Assert.AreEqual("",Patients.GetPreferredAndClinicChangeLogText(Pat(null,0),Pat("",0)));
			Assert.AreEqual("",Patients.GetPreferredAndClinicChangeLogText(Pat("",0),Pat(null,0)));
		}

		[TestMethod]
		public void PreferredName_CaseChange_IsLogged() {
			Assert.AreEqual("Patient preferred name changed from 'lex' to 'Lex'\r\n",
				Patients.GetPreferredAndClinicChangeLogText(Pat("lex",0),Pat("Lex",0)));
		}

		[TestMethod]
		public void NothingChanged_GivesNoText() {
			Assert.AreEqual("",Patients.GetPreferredAndClinicChangeLogText(Pat("Lex",CLINIC_SOUTH),Pat("Lex",CLINIC_SOUTH)));
		}

		[TestMethod]
		public void MissingPatient_GivesNoText() {
			Assert.AreEqual("",Patients.GetPreferredAndClinicChangeLogText(null,Pat("Lex",0)));
			Assert.AreEqual("",Patients.GetPreferredAndClinicChangeLogText(Pat("Lex",0),null));
		}

		[TestMethod]
		public void Clinic_Changed_NamesBothAbbreviations() {
			Assert.AreEqual("Patient clinic changed from 'NORTH' to 'SOUTH'\r\n",
				Patients.GetPreferredAndClinicChangeLogText(Pat("",CLINIC_NORTH),Pat("",CLINIC_SOUTH)));
		}

		[TestMethod]
		public void Clinic_Zero_IsNone() {
			Assert.AreEqual("Patient clinic changed from 'None' to 'NORTH'\r\n",Patients.GetClinicChangeLogLine(0,CLINIC_NORTH));
			Assert.AreEqual("Patient clinic changed from 'SOUTH' to 'None'\r\n",Patients.GetClinicChangeLogLine(CLINIC_SOUTH,0));
		}

		[TestMethod]
		public void Clinic_Same_GivesNoText() {
			Assert.AreEqual("",Patients.GetClinicChangeLogLine(CLINIC_NORTH,CLINIC_NORTH));
			Assert.AreEqual("",Patients.GetClinicChangeLogLine(0,0));
		}

		[TestMethod]
		public void Clinic_MissingOrBlankAbbreviation_ShowsClinicNum() {
			Assert.AreEqual("Patient clinic changed from 'ClinicNum 999' to 'SOUTH'\r\n",Patients.GetClinicChangeLogLine(CLINIC_MISSING,CLINIC_SOUTH));
			Assert.AreEqual("Patient clinic changed from 'NORTH' to 'ClinicNum 904'\r\n",Patients.GetClinicChangeLogLine(CLINIC_NORTH,CLINIC_BLANK));
		}

		[TestMethod]
		public void Clinic_SameAbbreviation_AddsClinicNums() {
			Assert.AreEqual("Patient clinic changed from 'NORTH (ClinicNum 901)' to 'NORTH (ClinicNum 903)'\r\n",
				Patients.GetClinicChangeLogLine(CLINIC_NORTH,CLINIC_TWIN));
		}

		[TestMethod]
		public void PreferredAndClinic_BothChanged_PreferredFirst() {
			Assert.AreEqual("Patient preferred name changed from 'Al' to 'Lex'\r\nPatient clinic changed from 'None' to 'SOUTH'\r\n",
				Patients.GetPreferredAndClinicChangeLogText(Pat("Al",0),Pat("Lex",CLINIC_SOUTH)));
		}
	}
}
