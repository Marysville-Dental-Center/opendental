﻿using OpenDentBusiness;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTestsCore {
	///<summary>Has methods for both SheetDef and SheetFieldDef. Use SheetT for sheets.</summary>
	public class EFormDefT {
		///<summary>Deletes everything from the sheetdef and sheetfielddef table.  Does not truncate the table so that PKs are not reused on accident.</summary>
		public static void ClearEFormDefAndEFormFieldDefTable() {
			string command="DELETE FROM eformdef WHERE eformdefnum > 0";
			DataCore.NonQ(command);
			command="DELETE FROM eformfielddef WHERE eformfielddefnum > 0";
			DataCore.NonQ(command);
			command="DELETE FROM eclipboardsheetdef WHERE eclipboardsheetdefnum > 0";
			DataCore.NonQ(command);
		}

		///<summary>Creates a simple SheetDef and inserts it into the database.</summary>
		public static EFormDef CreateEFormDef(EnumEFormType eformType,string description="") {
			EFormDef eformDef=new EFormDef();
			eformDef.IsNew=true;
			eformDef.FormType=eformType;
			eformDef.Description=description;
			eformDef.ListEFormFieldDefs=new List<EFormFieldDef>();
			EFormDefs.Insert(eformDef);
			EFormDefs.RefreshCache();
			return eformDef;
		}

		///<summary>Inserts a copy of the internal eFormDef with its field defs.</summary>
		public static EFormDef CreateCustomEForm(EnumEFormInternalType eFormInternalType) {
			EFormDef eFormDef=EFormInternal.GetEFormDef(eFormInternalType);
			eFormDef.IsNew=true;
			EFormDefs.Insert(eFormDef);
			//Link EFormFieldDefs to EFormDef
			for(int i = 0;i<eFormDef.ListEFormFieldDefs.Count;i++) {
				eFormDef.ListEFormFieldDefs[i].EFormDefNum=eFormDef.EFormDefNum;
			}
			EFormFieldDefs.Sync(eFormDef.ListEFormFieldDefs,eFormDef.EFormDefNum);
			EFormDefs.RefreshCache();
			EFormFieldDefs.RefreshCache();
			SheetDefs.RefreshCache();
			SheetFieldDefs.RefreshCache();
			return eFormDef;
		}
	}
}