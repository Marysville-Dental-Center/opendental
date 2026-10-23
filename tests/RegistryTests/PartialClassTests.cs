using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RegistryTests {
	///<summary>The six heavily shared data classes are partial, so later packages can add methods in new files (backlog new-file rule).</summary>
	[TestClass]
	public class PartialClassTests {
		[TestMethod]
		public void HotDataClasses_ArePartial() {
			foreach(string className in new[] { "DatabaseMaintenances","Procedures","ClaimProcs","Patients","Appointments","Statements" }) {
				string source=File.ReadAllText(TestPaths.GetRepoFile("OpenDentBusiness","Data Interface",className+".cs"));
				Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(source,@"public partial class "+className+@"\s*\{"),className+" must be partial.");
			}
		}
	}
}
