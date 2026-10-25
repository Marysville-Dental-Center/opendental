// B-PRINT: the statement inserter barcode in the sheet editor (Open Dental 25.3): how the Special field looks, and the clear-zone warning.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Drawing;
using CodeBase;
using OpenDentBusiness;

namespace OpenDental {
	public partial class FormSheetDefEdit {
		///<summary>A sample barcode (page 1 of 1 of the first statement) so the layout shows what will print there.</summary>
		private void DrawInserterBarcode(SheetFieldDef sheetFieldDef,Graphics g) {
			try {
				StatementInserterBarcode.Draw(g,new RectangleF(sheetFieldDef.XPos,sheetFieldDef.YPos,sheetFieldDef.Width,sheetFieldDef.Height),
					StatementInserterBarcode.GetContent(1,1,1));
			}
			catch(Exception ex) {
				ex.DoNothing();//A field too small to draw shows as its outline only.
			}
		}

		///<summary>An envelope inserter reads the barcode only when nothing prints over it. When the layout's barcode touches other fields, asks
		///whether to save anyway. True to go on saving.</summary>
		private bool ConfirmInserterBarcodeClearZone() {
			if(SheetDef_==null || SheetDef_.SheetType!=SheetTypeEnum.Statement) {
				return true;
			}
			List<SheetFieldDef> listOverlaps=StatementInserterBarcode.GetOverlappingFields(SheetDef_);
			if(listOverlaps.Count==0) {
				return true;
			}
			string message=Lan.g(this,"The inserter barcode overlaps other fields:")+" "+listOverlaps.Count+"\r\n"
				+Lan.g(this,"An envelope inserter may not read it unless the area around it is clear.")+"\r\n"
				+Lan.g(this,"Save anyway?");
			return MsgBox.Show(MsgBoxButtons.YesNo,message);
		}
	}
}
