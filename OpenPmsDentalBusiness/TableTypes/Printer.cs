using System;
using System.Collections;
using CodeBase;

namespace OpenPmsDentalBusiness{

	///<summary>One printer selection for one situation for one computer.</summary>
	[Serializable]
	public class Printer:TableBase {
		///<summary>Primary key.</summary>
		[CrudColumn(IsPriKey=true)]
		public long PrinterNum;
		///<summary>FK to computer.ComputerNum.  This will be changed some day to refer to the computername, because it would make more sense as a key than a cryptic number.</summary>
		public long ComputerNum;
		///<summary>Enum:PrintSituation One of about 10 different situations where printing takes place.  PrintSituation.Default is the program's own default situation, not the Windows default printer.  If no printer object exists for a situation, the Default situation's row applies, and without that the Windows default printer is used with no prompt (25.4 documentation; see PrintSituationRules).</summary>
		public PrintSituation PrintSit;
		///<summary>The name of the printer as set from the specified computer.  Empty means "use the Default situation's printer, or the Windows default".</summary>
		public string PrinterName;
		///<summary>If true, then user will be prompted for printer.  Otherwise, print directly with little user interaction.  A row must exist when this is true, even with no printer chosen.</summary>
		public bool DisplayPrompt;
		///<summary>String that holds the file extension type for this printer. No leading period. Example pdf or xps. Only used when IsVirtualPrinter is true.</summary>
		public string FileExtension;
		///<summary>Bool that indicates if this printer is a virtual  printer (pdf, xps, etc).</summary>
		public bool IsVirtualPrinter;

		///<summary>Returns a copy of the printer.</summary>
    public Printer Clone(){
			return (Printer)this.MemberwiseClone();
		}

		
	}



	



}









