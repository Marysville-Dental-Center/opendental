// Source checks for the birthdate audit (feature v26_1-tools-audit-birthdate). The audit sits in Patients.Insert and Patients.Update, so it covers
// every save path only while nothing writes patient rows around them. These tests read the repository's C# files; they need no database.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace M2AUDITTests {
	[TestClass]
	public class BirthdateSavePathTests {
		///<summary>Folders, relative to the repository root, whose code is not part of the program: tests, test helpers, build output, the frozen
		///legacy data classes, generated CRUD (only Patients.cs calls it for patients), and worktrees of other branches under .claude.</summary>
		private static readonly string[] _arrayExcludedFolders={ "bin","obj",".git",".claude","tests","UnitTests","UnitTestsCore","TestCanada",
			"DataClassesOld","OpenDentBusiness.Tests","NoHqTests" };
		private static List<string> _listSourceFiles;

		///<summary>Repository-relative paths of the program's C# files.</summary>
		private static List<string> GetSourceFiles() {
			if(_listSourceFiles!=null) {
				return _listSourceFiles;
			}
			string root=ScratchDb.GetRepositoryRoot();
			List<string> listFiles=new List<string>();
			foreach(string path in Directory.EnumerateFiles(root,"*.cs",SearchOption.AllDirectories)) {
				string relative=path.Substring(root.Length).TrimStart('\\','/');
				string[] arrayParts=relative.Split('\\','/');
				if(arrayParts.Take(arrayParts.Length-1).Any(x => _arrayExcludedFolders.Contains(x,StringComparer.OrdinalIgnoreCase))) {
					continue;
				}
				if(relative.StartsWith(Path.Combine("OpenDentBusiness","Crud")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				//The frozen legacy convert chain (24.3.63 and older) repairs data during an upgrade; it is not a save path.
				if(Regex.IsMatch(relative,@"^OpenDentBusiness[\\/]Misc[\\/]ConvertDatabases\d*\.cs$",RegexOptions.IgnoreCase)) {
					continue;
				}
				listFiles.Add(relative);
			}
			_listSourceFiles=listFiles;
			return listFiles;
		}

		private static string ReadRepoFile(params string[] arrayParts) {
			return File.ReadAllText(Path.Combine(new[] { ScratchDb.GetRepositoryRoot() }.Concat(arrayParts).ToArray()));
		}

		[TestMethod]
		public void PatientRowsAreWrittenOnlyByPatientsInsertAndUpdate() {
			Regex regex=new Regex(@"(?<![A-Za-z0-9_])PatientCrud\.(Insert|InsertMany|InsertNoCache|Update|Sync)\s*\(");
			string pathPatients=Path.Combine("OpenDentBusiness","Data Interface","Patients.cs");
			List<string> listViolations=new List<string>();
			foreach(string relative in GetSourceFiles()) {
				if(relative.Equals(pathPatients,StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				string text=File.ReadAllText(Path.Combine(ScratchDb.GetRepositoryRoot(),relative));
				if(regex.IsMatch(text)) {
					listViolations.Add(relative);
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Write patients through Patients.Insert or Patients.Update, so birthdate changes are audited: "
				+string.Join(", ",listViolations));
		}

		[TestMethod]
		public void NoRawSqlWritesTheBirthdate() {
			//An UPDATE patient ... SET or INSERT INTO patient statement that names Birthdate before its WHERE (or the end of the C# statement).
			Regex regexStart=new Regex(@"(?i)(UPDATE\s+patient\s|INSERT\s+INTO\s+patient\s*[\(\s])");
			List<string> listViolations=new List<string>();
			foreach(string relative in GetSourceFiles()) {
				string text=File.ReadAllText(Path.Combine(ScratchDb.GetRepositoryRoot(),relative));
				foreach(Match match in regexStart.Matches(text)) {
					int indexEnd=text.IndexOf(';',match.Index);
					if(indexEnd<0) {
						indexEnd=text.Length;
					}
					string statement=text.Substring(match.Index,indexEnd-match.Index);
					int indexWhere=statement.IndexOf("WHERE",StringComparison.OrdinalIgnoreCase);
					if(indexWhere>=0) {
						statement=statement.Substring(0,indexWhere);
					}
					if(Regex.IsMatch(statement,@"(?i)\bBirthdate\b")) {
						listViolations.Add(relative+" at "+match.Index);
					}
				}
			}
			Assert.AreEqual(0,listViolations.Count,"Raw SQL writes patient.Birthdate without the audit: "+string.Join(", ",listViolations));
		}

		[TestMethod]
		public void HooksSitInInsertAndUpdate() {
			//Right after the row is written, on the server side of the Middle Tier check.
			string text=ReadRepoFile("OpenDentBusiness","Data Interface","Patients.cs");
			Assert.IsTrue(Regex.IsMatch(text,@"Crud\.PatientCrud\.Update\(pat\);\s*InsertBirthdateChangeSecurityLogEntry\(null,pat\);"),"Patients.Insert");
			Assert.IsTrue(Regex.IsMatch(text,@"bool retval=Crud\.PatientCrud\.Update\(patient,oldPatient\);\s*InsertBirthdateChangeSecurityLogEntry\(oldPatient,patient\);"),
				"Patients.Update");
		}

		[TestMethod]
		public void EditPatientInformation_SavesThroughPatientsUpdate() {
			StringAssert.Contains(ReadRepoFile("OpenDental","Forms","FormPatientEdit.cs"),"Patients.SavePatientEdit(");
			Regex regex=new Regex(@"public static Result SavePatientEdit\([^)]*\)\s*\{\s*Result result=new Result\(\);\s*Update\(patient,patientOld\);");
			Assert.IsTrue(regex.IsMatch(ReadRepoFile("OpenDentBusiness","Data Interface","Patients.cs")),
				"Patients.SavePatientEdit saves the patient with Update(patient,patientOld).");
		}

		[TestMethod]
		public void SheetImport_SavesTheBirthdateThroughPatientsUpdate() {
			string text=ReadRepoFile("OpenDental","Forms","FormSheetImport.cs");
			StringAssert.Contains(text,"_patient.Birthdate=(DateTime)_listImportRows[i].ImpValObj;");
			StringAssert.Contains(text,"Patients.Update(_patient,_patientOld);");
		}

		[TestMethod]
		public void AddFamilyWindow_InsertsThroughPatientsInsert() {
			string text=ReadRepoFile("WpfControlsOD","Frms","FrmPatientAddAll.xaml.cs");
			int indexInsert=text.IndexOf("long patNum=Patients.Insert(patient,false);");
			Assert.IsTrue(indexInsert>=0,"The Add Family window inserts each new patient with Patients.Insert.");
			int indexBirthdate=text.IndexOf("patient.Birthdate=PIn.Date(textBirthdate1.Text);");
			Assert.IsTrue(indexBirthdate>=0 && indexBirthdate<indexInsert,"The Add Family window sets the birthdate before it inserts the patient.");
		}

		[TestMethod]
		public void EFormImport_SavesThroughPatientsUpdate() {
			string text=ReadRepoFile("OpenDentBusiness","EForms","EFormImport.cs");
			StringAssert.Contains(text,"case \"Birthdate\":");
			StringAssert.Contains(text,"Patients.Update(patient,patientOld);");
		}
	}
}
