// B-PRINT: Export .csv and Export .txt (Open Dental 24.4 Transaction History, 25.3 "more windows"). Text rules of OpenDental.UI.GridODExport.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental.UI;

namespace BPRINTTests {
	[TestClass]
	public class GridExportTests {
		private static readonly List<string> _listHeadings=new List<string> { "Date","Description","Amount" };

		[TestMethod]
		public void Csv_QuotesOnlyFieldsThatNeedIt_AndDoublesQuotes() {
			Assert.AreEqual("plain",GridODExport.FormatField("plain",GridExportFormat.Csv));
			Assert.AreEqual("\"Smith, John\"",GridODExport.FormatField("Smith, John",GridExportFormat.Csv));
			Assert.AreEqual("\"A \"\"quoted\"\" word\"",GridODExport.FormatField("A \"quoted\" word",GridExportFormat.Csv));
			Assert.AreEqual("\"two\r\nlines\"",GridODExport.FormatField("two\r\nlines",GridExportFormat.Csv));
			Assert.AreEqual("\"line\nfeed\"",GridODExport.FormatField("line\nfeed",GridExportFormat.Csv));
			Assert.AreEqual("tab\tkept",GridODExport.FormatField("tab\tkept",GridExportFormat.Csv),"A tab is ordinary text in a .csv file.");
			Assert.AreEqual("",GridODExport.FormatField(null,GridExportFormat.Csv));
		}

		[TestMethod]
		public void Txt_ReplacesTabsAndLineBreaksWithSpaces_AndNeverQuotes() {
			Assert.AreEqual("a b",GridODExport.FormatField("a\tb",GridExportFormat.Txt));
			Assert.AreEqual("two lines",GridODExport.FormatField("two\r\nlines",GridExportFormat.Txt));
			Assert.AreEqual("cr only",GridODExport.FormatField("cr\ronly",GridExportFormat.Txt));
			Assert.AreEqual("Smith, \"John\"",GridODExport.FormatField("Smith, \"John\"",GridExportFormat.Txt));
		}

		[TestMethod]
		public void Fields_ThatASpreadsheetWouldRunAsAFormula_GetALeadingSpace() {
			Assert.AreEqual(" =SUM(A1:A2)",GridODExport.ProtectFromFormula("=SUM(A1:A2)"));
			Assert.AreEqual(" +1+1",GridODExport.ProtectFromFormula("+1+1"));
			Assert.AreEqual(" @cmd",GridODExport.ProtectFromFormula("@cmd"));
			Assert.AreEqual(" -Adjustment",GridODExport.ProtectFromFormula("-Adjustment"));
			Assert.AreEqual("-12.50",GridODExport.ProtectFromFormula("-12.50"),"A negative amount stays a number.");
			Assert.AreEqual("-",GridODExport.ProtectFromFormula("-"));
			Assert.AreEqual("",GridODExport.ProtectFromFormula(""));
			Assert.AreEqual("\" =1,2\"",GridODExport.FormatField("=1,2",GridExportFormat.Csv),"Protected first, then quoted.");
		}

		[TestMethod]
		public void BuildText_HeadingRowThenOneLinePerRow_WithCrlf() {
			List<IList<string>> listRows=new List<IList<string>> {
				new List<string> { "10/01/2026","Check, #100","-25.00" },
				new List<string> { "10/02/2026","Deposit","100.00" },
			};
			string csv=GridODExport.BuildText(_listHeadings,listRows,GridExportFormat.Csv);
			Assert.AreEqual("Date,Description,Amount\r\n10/01/2026,\"Check, #100\",-25.00\r\n10/02/2026,Deposit,100.00\r\n",csv);
			string txt=GridODExport.BuildText(_listHeadings,listRows,GridExportFormat.Txt);
			Assert.AreEqual("Date\tDescription\tAmount\r\n10/01/2026\tCheck, #100\t-25.00\r\n10/02/2026\tDeposit\t100.00\r\n",txt);
		}

		[TestMethod]
		public void BuildText_PadsShortRows_AndDropsExtraCells() {
			List<IList<string>> listRows=new List<IList<string>> {
				new List<string> { "only one" },
				new List<string> { "1","2","3","4" },
				null,
			};
			string csv=GridODExport.BuildText(_listHeadings,listRows,GridExportFormat.Csv);
			Assert.AreEqual("Date,Description,Amount\r\nonly one,,\r\n1,2,3\r\n,,\r\n",csv);
		}

		[TestMethod]
		public void BuildText_FromAGrid_UsesHeadingsAndEveryRow() {
			using(GridOD gridOD=new GridOD()) {
				gridOD.Columns.Add(new GridColumn("Name",100));
				gridOD.Columns.Add(new GridColumn("Note",100));
				gridOD.ListGridRows.Add(new GridRow("Jones","first\r\nsecond"));
				gridOD.ListGridRows.Add(new GridRow("Lee","a,b"));
				Assert.AreEqual("Name,Note\r\nJones,\"first\r\nsecond\"\r\nLee,\"a,b\"\r\n",GridODExport.BuildText(gridOD,GridExportFormat.Csv));
				Assert.AreEqual("Name\tNote\r\nJones\tfirst second\r\nLee\ta,b\r\n",GridODExport.BuildText(gridOD,GridExportFormat.Txt));
			}
		}

		[TestMethod]
		public void WriteFile_IsUtf8WithByteOrderMark_SoAccentsSurvive() {
			string path=Path.Combine(Path.GetTempPath(),"bprint_export_"+System.Guid.NewGuid().ToString("N")+".csv");
			try {
				GridODExport.WriteFile(path,"Name\r\nMuñoz\r\n");
				byte[] arrayBytes=File.ReadAllBytes(path);
				CollectionAssert.AreEqual(new byte[] { 0xEF,0xBB,0xBF },arrayBytes.Take(3).ToArray(),"UTF-8 byte order mark.");
				Assert.AreEqual("Name\r\nMuñoz\r\n",new UTF8Encoding(true).GetString(arrayBytes,3,arrayBytes.Length-3));
			}
			finally {
				File.Delete(path);
			}
		}

		[TestMethod]
		public void FileName_Extension_Filter_AndDelimiter_FollowTheFormat() {
			Assert.AreEqual(".csv",GridODExport.GetExtension(GridExportFormat.Csv));
			Assert.AreEqual(".txt",GridODExport.GetExtension(GridExportFormat.Txt));
			Assert.AreEqual(",",GridODExport.GetDelimiter(GridExportFormat.Csv));
			Assert.AreEqual("\t",GridODExport.GetDelimiter(GridExportFormat.Txt));
			StringAssert.StartsWith(GridODExport.GetFilter(GridExportFormat.Csv),"CSV files (*.csv)|*.csv");
			StringAssert.StartsWith(GridODExport.GetFilter(GridExportFormat.Txt),"Text files (*.txt)|*.txt");
			Assert.AreEqual("Checking - Asset.csv",GridODExport.GetSuggestedFileName("Checking - Asset",GridExportFormat.Csv));
			Assert.AreEqual("AR 102026.txt",GridODExport.GetSuggestedFileName("AR 10/2026",GridExportFormat.Txt),"Characters Windows refuses are dropped.");
			Assert.AreEqual("Employees.csv",GridODExport.GetSuggestedFileName("Employees.csv",GridExportFormat.Csv),"No doubled extension.");
			Assert.AreEqual("Export.txt",GridODExport.GetSuggestedFileName("",GridExportFormat.Txt));
			Assert.AreEqual("Export.csv",GridODExport.GetSuggestedFileName(null,GridExportFormat.Csv));
		}
	}
}
