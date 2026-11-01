// Shared helpers for the V-BRIDGE2 tests: repository paths and synthetic patients.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using OpenPmsDentalBusiness;

namespace VBRIDGE2Tests {
	///<summary>Locates the repository.</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds the repository root (LICENSE and NOTICE).</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"LICENSE")) && File.Exists(Path.Combine(dir,"NOTICE"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (LICENSE and NOTICE) above the test assembly.");
		}

		///<summary>The text of a file under the repository root, for example ReadRepoFile("WpfControlsOD","Logic","ProgramL.cs").</summary>
		public static string ReadRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return File.ReadAllText(path);
		}
	}

	///<summary>Made-up patients. No office data.</summary>
	public static class SyntheticPatients {
		public static Patient Create() {
			Patient patient=new Patient();
			patient.PatNum=1042;
			patient.LName="O'Testa";
			patient.FName="Ann \"Annie\"";
			patient.MiddleI="Q";
			patient.Preferred="";
			patient.ChartNumber="CH-77";
			patient.Birthdate=new DateTime(1980,3,9);
			patient.Address="12 Sample St";
			patient.Address2="Unit 4";
			patient.City="Exampleton";
			patient.State="WA";
			patient.Zip="98000";
			patient.HmPhone="(555)010-0001";
			patient.WirelessPhone="(555)010-0002";
			patient.WkPhone="";
			patient.Email="";
			patient.SSN="900000001";
			patient.Gender=PatientGender.Female;
			patient.Title="";
			return patient;
		}
	}
}
