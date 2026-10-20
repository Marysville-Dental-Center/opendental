// Test helpers for the DentalXChange tests: repository files and the Open Dental 25.3.48 reference schema.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.IO;
using System.Text.RegularExpressions;
using VendorDirectTests.Foundation;

namespace VendorDirectTests.DentalXChange {
	public static class DxcTestFiles {
		public static string ReadRepoFile(string relativePath) {
			return File.ReadAllText(Path.Combine(FoundationLayoutTests.GetRepositoryRoot(),relativePath));
		}

		///<summary>The CREATE TABLE statement for tableName from the Open Dental 25.3.48 reference schema the schema verifier uses.</summary>
		public static string GetReferenceCreateTable(string tableName) {
			string sql=ReadRepoFile(@"OpenDentBusiness\Misc\ForkMigrations\Reference\OdSchema-25.3.48.sql");
			Match match=Regex.Match(sql,@"CREATE TABLE `"+Regex.Escape(tableName)+@"` \(.*?\) ENGINE=[^;]*;",RegexOptions.Singleline);
			if(!match.Success) {
				throw new InvalidOperationException("Table "+tableName+" is not in the reference schema.");
			}
			return match.Value;
		}
	}
}
