using OpenDentBusiness;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTestsCore {
	///<summary>Has methods for both SheetDef and SheetFieldDef. Use SheetT for sheets.</summary>
	public class SheetDefT {
		///<summary>Deletes everything from the sheetdef and sheetfielddef table.  Does not truncate the table so that PKs are not reused on accident.</summary>
		public static void ClearSheetDefAndSheetFieldDefTable() {
			string command="DELETE FROM sheetdef WHERE sheetdefnum > 0";
			DataCore.NonQ(command);
			command="DELETE FROM sheetfielddef WHERE sheetfielddefnum > 0";
			DataCore.NonQ(command);
			command="DELETE FROM eclipboardsheetdef WHERE eclipboardsheetdefnum > 0";
			DataCore.NonQ(command);
		}

		///<summary>Creates a simple SheetDef and inserts it into the database.</summary>
		public static SheetDef CreateSheetDef(SheetTypeEnum sheetType,string description="") {
			SheetDef sheetDef=new SheetDef();
			sheetDef.IsNew=true;
			sheetDef.SheetType=sheetType;
			sheetDef.Description=description;
			sheetDef.SheetFieldDefs=new List<SheetFieldDef>();
			SheetDefs.InsertOrUpdate(sheetDef);
			SheetDefs.RefreshCache();
			return sheetDef;
		}

		///<summary>Inserts a copy of the internal sheetDef.</summary>
		public static SheetDef CreateCustomSheet(SheetInternalType sheetInternalType) {
			var sheetDef=SheetDefs.GetInternalOrCustom(sheetInternalType);
			sheetDef.IsNew=true;
			SheetDefs.InsertOrUpdate(sheetDef);
			EFormDefs.RefreshCache();
			EFormFieldDefs.RefreshCache();
			SheetDefs.RefreshCache();
			SheetFieldDefs.RefreshCache();
			return sheetDef;
		}
	}
}
