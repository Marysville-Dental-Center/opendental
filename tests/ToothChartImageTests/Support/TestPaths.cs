// Folders of the repository that the chart image tests read and write.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;

namespace ToothChartImageTests {
	///<summary>MSTest v1 has no Assert.ThrowsException.</summary>
	internal static class Expect {
		public static T Throws<T>(Action action,string message="") where T:Exception {
			try {
				action();
			}
			catch(T ex) {
				return ex;
			}
			catch(Exception ex) {
				Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+" but got "+ex.GetType().Name+": "+ex.Message+" "+message);
			}
			Microsoft.VisualStudio.TestTools.UnitTesting.Assert.Fail("Expected "+typeof(T).Name+" but nothing was thrown. "+message);
			return null;
		}
	}

	///<summary>The repository root is found from the test assembly's folder (tests\ToothChartImageTests\bin\Debug), so the tests work in any
	///worktree.</summary>
	public static class TestPaths {
		private static string _repoRoot;

		///<summary>The first folder above the test assembly that holds LICENSE and NOTICE (not found by the solution's name, which a rename may
		///change; package REN-PROOF).</summary>
		public static string RepoRoot {
			get {
				if(_repoRoot!=null) {
					return _repoRoot;
				}
				string folder=AppDomain.CurrentDomain.BaseDirectory;
				while(folder!=null && !(File.Exists(Path.Combine(folder,"LICENSE")) && File.Exists(Path.Combine(folder,"NOTICE")))) {
					folder=Path.GetDirectoryName(folder.TrimEnd('\\'));
				}
				if(folder==null) {
					throw new InvalidOperationException("The repository root (LICENSE and NOTICE) was not found above "+AppDomain.CurrentDomain.BaseDirectory);
				}
				_repoRoot=folder.TrimEnd('\\');
				return _repoRoot;
			}
		}

		///<summary>tests\ToothChartImageTests.</summary>
		public static string ProjectFolder {
			get {
				return Path.Combine(RepoRoot,"tests","ToothChartImageTests");
			}
		}

		///<summary>tests\ToothChartImageTests\Baselines\legacy: the pictures of today's DirectX, OpenGL and Simple 2D charts (package TC-0).</summary>
		public static string BaselinesLegacy {
			get {
				return Path.Combine(ProjectFolder,"Baselines","legacy");
			}
		}

		///<summary>tests\ToothChartImageTests\Baselines\legacy-smoothed: a few DirectX pictures drawn multisampled, as the program draws them, for the
		///gallery only (LegacyRecorder.ListSmoothed). Never compared.</summary>
		public static string BaselinesLegacySmoothed {
			get {
				return LegacyRecorder.GetSmoothedFolder(BaselinesLegacy);
			}
		}

		///<summary>SparksToothChart\Resources: the .obj models that SparksToothChart embeds.</summary>
		public static string ModelsFolder {
			get {
				return Path.Combine(RepoRoot,"SparksToothChart","Resources");
			}
		}

		///<summary>Reads a repository file by its path relative to the root, with either kind of slash.</summary>
		public static string ReadSource(string pathRelative) {
			return File.ReadAllText(Path.Combine(RepoRoot,pathRelative.Replace('/','\\')));
		}
	}
}
