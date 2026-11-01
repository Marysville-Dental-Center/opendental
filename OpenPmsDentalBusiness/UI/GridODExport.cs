// B-PRINT: Export .csv and Export .txt for grids (Open Dental 24.4 Transaction History, 25.3 "more windows").
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Forms;
using CodeBase;
using OpenPmsDentalBusiness;

namespace OpenPmsDental.UI {
	///<summary>The two file formats a grid can be exported to.</summary>
	public enum GridExportFormat {
		///<summary>Comma-separated values (RFC 4180), .csv.</summary>
		Csv,
		///<summary>Tab-separated text, .txt.</summary>
		Txt,
	}

	///<summary>Export of a GridOD to a .csv or .txt file. The text rules are plain static methods so they can be tested without a window:
	///- .csv follows RFC 4180: a field that holds a comma, a double quote or a line break is put in double quotes, and quotes inside it are doubled.
	///- .txt is tab-separated; tabs and line breaks inside a field become spaces. Fields are never quoted.
	///- The first row holds the column headings. Lines end with CRLF. The file is UTF-8 with a byte order mark, so spreadsheet programs read accents.
	///- A field that a spreadsheet program would read as a formula (it starts with =, + or @, or with - when it is not a number) gets a leading
	///space, the way the 24.3 Chart of Accounts export already treated such headings.
	///The window part (ExportAs, ShowExportMenu) asks where to save, starting in the ExportPath preference folder, as GridOD.Export does.</summary>
	public static class GridODExport {
		///<summary>Menu and button text. Translated through Lans with the "GridOD" category.</summary>
		public const string TEXT_EXPORT_CSV="Export .csv";
		public const string TEXT_EXPORT_TXT="Export .txt";
		///<summary>The anchor control's export menu, kept for the life of the control so it is created once and disposed with it.</summary>
		private static readonly ConditionalWeakTable<Control,ExportMenu> _tableExportMenus=new ConditionalWeakTable<Control,ExportMenu>();

		#region Text rules
		///<summary>".csv" or ".txt".</summary>
		public static string GetExtension(GridExportFormat gridExportFormat) {
			if(gridExportFormat==GridExportFormat.Csv) {
				return ".csv";
			}
			return ".txt";
		}

		///<summary>The save dialog filter for the format, with All files as the second choice.</summary>
		public static string GetFilter(GridExportFormat gridExportFormat) {
			if(gridExportFormat==GridExportFormat.Csv) {
				return "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
			}
			return "Text files (*.txt)|*.txt|All files (*.*)|*.*";
		}

		///<summary>The delimiter between fields: a comma for .csv, a tab for .txt.</summary>
		public static string GetDelimiter(GridExportFormat gridExportFormat) {
			if(gridExportFormat==GridExportFormat.Csv) {
				return ",";
			}
			return "\t";
		}

		///<summary>One field (a heading or a cell) as it is written to the file.</summary>
		public static string FormatField(string value,GridExportFormat gridExportFormat) {
			string text=ProtectFromFormula(value??"");
			if(gridExportFormat==GridExportFormat.Txt) {
				return text.Replace("\r\n"," ").Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
			}
			if(text.IndexOfAny(new[] { ',','"','\r','\n' })<0) {
				return text;
			}
			return "\""+text.Replace("\"","\"\"")+"\"";
		}

		///<summary>Adds a leading space to text that a spreadsheet program would otherwise run as a formula: text starting with =, + or @, or with -
		///when the text is not a number (so -12.50 stays a number).</summary>
		public static string ProtectFromFormula(string text) {
			if(string.IsNullOrEmpty(text)) {
				return text??"";
			}
			char charFirst=text[0];
			if(charFirst=='=' || charFirst=='+' || charFirst=='@') {
				return " "+text;
			}
			if(charFirst=='-' && text.Length>1 && !IsNumber(text)) {
				return " "+text;
			}
			return text;
		}

		private static bool IsNumber(string text) {
			return decimal.TryParse(text,NumberStyles.Any,CultureInfo.CurrentCulture,out decimal _)
				|| decimal.TryParse(text,NumberStyles.Any,CultureInfo.InvariantCulture,out decimal _);
		}

		///<summary>The whole file: a heading row, then one line per row, each ending with CRLF. A row with fewer cells than headings is padded with
		///empty fields; extra cells are left out.</summary>
		public static string BuildText(IList<string> listHeadings,IEnumerable<IList<string>> rows,GridExportFormat gridExportFormat) {
			string delimiter=GetDelimiter(gridExportFormat);
			StringBuilder stringBuilder=new StringBuilder();
			stringBuilder.Append(string.Join(delimiter,listHeadings.Select(x => FormatField(x,gridExportFormat))));
			stringBuilder.Append("\r\n");
			foreach(IList<string> listCells in rows) {
				List<string> listFields=new List<string>();
				for(int i=0;i<listHeadings.Count;i++) {
					string cell="";
					if(listCells!=null && i<listCells.Count) {
						cell=listCells[i];
					}
					listFields.Add(FormatField(cell,gridExportFormat));
				}
				stringBuilder.Append(string.Join(delimiter,listFields));
				stringBuilder.Append("\r\n");
			}
			return stringBuilder.ToString();
		}

		///<summary>The column headings of the grid.</summary>
		public static List<string> GetHeadings(GridOD gridOD) {
			List<string> listHeadings=new List<string>();
			for(int i=0;i<gridOD.Columns.Count;i++) {
				listHeadings.Add(gridOD.Columns[i].Heading);
			}
			return listHeadings;
		}

		///<summary>The cell text of every row in the grid, one list per row, in grid order.</summary>
		public static List<IList<string>> GetRows(GridOD gridOD) {
			List<IList<string>> listRows=new List<IList<string>>();
			for(int i=0;i<gridOD.ListGridRows.Count;i++) {
				listRows.Add(gridOD.ListGridRows[i].Cells.Select(x => x.Text).ToList());
			}
			return listRows;
		}

		///<summary>The grid as file text.</summary>
		public static string BuildText(GridOD gridOD,GridExportFormat gridExportFormat) {
			return BuildText(GetHeadings(gridOD),GetRows(gridOD),gridExportFormat);
		}

		///<summary>Writes the text as UTF-8 with a byte order mark. Throws if the file cannot be written.</summary>
		public static void WriteFile(string filePath,string text) {
			File.WriteAllText(filePath,text,new UTF8Encoding(encoderShouldEmitUTF8Identifier:true));
		}

		///<summary>The suggested file name, without characters Windows does not allow and with the format's extension. "Export" when nothing is left.</summary>
		public static string GetSuggestedFileName(string fileName,GridExportFormat gridExportFormat) {
			string name=fileName??"";
			char[] arrayCharsInvalid=Path.GetInvalidFileNameChars();
			name=new string(name.Where(x => !arrayCharsInvalid.Contains(x)).ToArray()).Trim();
			string extension=GetExtension(gridExportFormat);
			if(name.EndsWith(extension,StringComparison.OrdinalIgnoreCase)) {
				name=name.Substring(0,name.Length-extension.Length).Trim();
			}
			if(name=="") {
				name="Export";
			}
			return name+extension;
		}
		#endregion Text rules

		#region Windows
		///<summary>Asks where to save, then writes every row of the grid in the chosen format. Shows "Nothing to export" for an empty grid.</summary>
		public static void ExportAs(this GridOD gridOD,string fileName,GridExportFormat gridExportFormat) {
			if(gridOD.ListGridRows.Count==0) {
				MessageBox.Show(Lans.g("GridOD","Nothing to export"));
				return;
			}
			string filePath;
			using(SaveFileDialog saveFileDialog=new SaveFileDialog()) {
				saveFileDialog.AddExtension=true;
				saveFileDialog.DefaultExt=GetExtension(gridExportFormat).TrimStart('.');
				saveFileDialog.Filter=GetFilter(gridExportFormat);
				saveFileDialog.FilterIndex=1;
				saveFileDialog.FileName=GetSuggestedFileName(fileName,gridExportFormat);
				string exportPath=PrefC.GetString(PrefName.ExportPath);
				try {
					if(!string.IsNullOrWhiteSpace(exportPath) && !Directory.Exists(exportPath)) {
						Directory.CreateDirectory(exportPath);
					}
					if(Directory.Exists(exportPath)) {
						saveFileDialog.InitialDirectory=exportPath;
					}
				}
				catch(Exception ex) {
					ex.DoNothing();//The dialog opens in its own default folder.
				}
				if(saveFileDialog.ShowDialog()!=DialogResult.OK) {
					return;
				}
				filePath=saveFileDialog.FileName;
			}
			try {
				WriteFile(filePath,BuildText(gridOD,gridExportFormat));
			}
			catch(Exception ex) {
				ex.DoNothing();
				MessageBox.Show(Lans.g("GridOD","File in use by another program.  Close and try again."));
				return;
			}
			MessageBox.Show(Lans.g("GridOD","File created successfully"));
		}

		///<summary>Shows a small menu under the control (usually the window's Export button) with Export .csv and Export .txt.</summary>
		public static void ShowExportMenu(this GridOD gridOD,Control controlAnchor,string fileName) {
			ExportMenu exportMenu=_tableExportMenus.GetValue(controlAnchor,x => new ExportMenu(x));
			exportMenu.GridOD_=gridOD;
			exportMenu.FileName=fileName;
			exportMenu.ContextMenuStrip_.Show(controlAnchor,new Point(0,controlAnchor.Height));
		}

		///<summary>One export menu per anchor control.</summary>
		private class ExportMenu {
			public ContextMenuStrip ContextMenuStrip_;
			public GridOD GridOD_;
			public string FileName;

			public ExportMenu(Control controlAnchor) {
				ContextMenuStrip_=new ContextMenuStrip();
				ContextMenuStrip_.Items.Add(Lans.g("GridOD",TEXT_EXPORT_CSV),null,(sender,e) => GridOD_?.ExportAs(FileName,GridExportFormat.Csv));
				ContextMenuStrip_.Items.Add(Lans.g("GridOD",TEXT_EXPORT_TXT),null,(sender,e) => GridOD_?.ExportAs(FileName,GridExportFormat.Txt));
				controlAnchor.Disposed+=(sender,e) => ContextMenuStrip_.Dispose();
			}
		}
		#endregion Windows
	}
}
