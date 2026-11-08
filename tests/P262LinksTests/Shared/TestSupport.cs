// Shared helpers for the P262-LINKS tests: repository paths, source reading and synthetic patients.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262LinksTests {
	///<summary>Locates the repository and reads its files.</summary>
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

		///<summary>The full path of a file under the repository root, for example GetRepoFile("WpfControlsOD","Logic","ProgramL.cs").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		public static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(GetRepoFile(arrayParts));
		}

		///<summary>The text of a method, from its signature to the closing brace at the signature's indent (sources use tabs).</summary>
		public static string GetMethodBody(string source,string signature) {
			int start=source.IndexOf(signature,StringComparison.Ordinal);
			Assert.IsTrue(start>=0,"Not found: "+signature);
			int lineStart=source.LastIndexOf('\n',start)+1;
			string indent=source.Substring(lineStart,start-lineStart);
			int end=source.IndexOf("\n"+indent+"}",start,StringComparison.Ordinal);
			Assert.IsTrue(end>start,"No end for: "+signature);
			return source.Substring(start,end-start);
		}
	}

	///<summary>Made-up patients. No office data.</summary>
	public static class SyntheticPatients {
		///<summary>Accented letters (the OrthoComposite page says they are allowed) and double quotes (removed on a command line).</summary>
		public const string LName="Núñez \"Nico\"";
		public const string FName="Zoé";

		public static Patient Create() {
			Patient patient=new Patient();
			patient.PatNum=1042;
			patient.LName=LName;
			patient.FName=FName;
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
			patient.Email=" first@example.com , second@example.com";
			patient.SSN="900000001";
			patient.Gender=PatientGender.Female;
			patient.Title="";
			return patient;
		}
	}
}
