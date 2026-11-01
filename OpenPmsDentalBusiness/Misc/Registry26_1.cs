// Registry entries for Open Dental 25.4 and 26.1 (package M2-02 in docs/specs/port-backlog.md) that live outside the enum files: definition
// category numbers whose enum member another package adds, and the Display Fields lists of the 25.4 and 26.1 display-field items.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenPmsDentalBusiness {
	///<summary>Definition categories added by Open Dental 26.1 (definition.Category, OpenDentalDiffDocumentation26-1.xml). The DefCat members
	///themselves belong to package M2-01, which owns Def.cs; the setup code (DefL, Defs) uses these constants. Since M2-01 is merged, each
	///constant is the DefCat member itself.</summary>
	public static class DefCats26_1 {
		///<summary>56 - Employee Titles: job titles for employee.Title.</summary>
		public const DefCat EmployeeTitles=DefCat.EmployeeTitles;

		///<summary>True when the DefCat enum already has a member for 56, so the category shows in Setup, Definitions (FormDefinitions lists the
		///enum members).</summary>
		public static bool IsEmployeeTitlesDefined() {
			return Enum.IsDefined(typeof(DefCat),(int)EmployeeTitles);
		}
	}

	///<summary>Display Fields entries for the Open Dental 25.4 and 26.1 display-field items. DisplayFields.GetDefaultList and GetAllAvailableList call
	///into this class after their own switch, so these lists add to whatever the switch built and never duplicate an entry another change already
	///added there.</summary>
	public static class DisplayFields26_1 {
		///<summary>Chart module, Patient Information: the patient's gender (Open Dental 26.1). Same internal name as the Family module's field.</summary>
		public const string ChartPatientInfoGender="Gender";
		///<summary>Appointment bubble: a "PreAuth" tag when the appointment holds a procedure its carrier wants preauthorized (Open Dental 26.1,
		///carrier.PreAuthCodes). Internal name as the 26.1 announcement spells the field.</summary>
		public const string ApptBubblePreAuthFlag="PreAuthFlag";
		///<summary>Shown text for the Treatment Plan column whose internal name is "Cat% UCR" (DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR).
		///Open Dental 25.4 says "standard provider fee" instead of "UCR" in user-visible text; the internal name, which stored rows use, stays.</summary>
		public const string TreatPlanCatPercStdFeeText="Cat% Std Fee";

		///<summary>Account module, Service Date View (DisplayFieldCategory 22, published by Open Dental 25.3 as a placeholder for 25.4): the columns of
		///the 24.3 Service Date View window, in its order and widths. Clinic is available but not shown by default.</summary>
		private static readonly List<(string InternalName,int ColumnWidth)> _listServiceDateViewDefault=new List<(string,int)> {
			("Service Date",90),("Trans Date",80),("Patient",150),("Reference",220),("Charge",80),("Production",80),("Prov",80),("InsBal",80),("AcctBal",80),
		};
		private static readonly List<(string InternalName,int ColumnWidth)> _listServiceDateViewExtra=new List<(string,int)> {
			("Clinic",80),
		};

		///<summary>Internal names of the Service Date View columns shown by default.</summary>
		public static List<string> GetServiceDateViewDefaultNames() {
			return _listServiceDateViewDefault.Select(x => x.InternalName).ToList();
		}

		///<summary>Called at the end of DisplayFields.GetDefaultList. Fills the default list of a 25.4 or 26.1 category when the switch left it empty.</summary>
		public static void AddDefaultFields(DisplayFieldCategory displayFieldCategory,List<DisplayField> listDisplayFields) {
			if(displayFieldCategory==DisplayFieldCategory.AccountModuleServiceDateView && listDisplayFields.Count==0) {
				AddRange(listDisplayFields,_listServiceDateViewDefault,displayFieldCategory);
			}
		}

		///<summary>Called at the end of DisplayFields.GetAllAvailableList. Adds the 25.4 and 26.1 fields to the available list of their category.</summary>
		public static void AddAvailableFields(DisplayFieldCategory displayFieldCategory,List<DisplayField> listDisplayFields) {
			switch(displayFieldCategory) {
				case DisplayFieldCategory.AccountModuleServiceDateView:
					AddRange(listDisplayFields,_listServiceDateViewDefault,displayFieldCategory);
					AddRange(listDisplayFields,_listServiceDateViewExtra,displayFieldCategory);
					break;
				case DisplayFieldCategory.ChartPatientInformation:
					AddIfMissing(listDisplayFields,ChartPatientInfoGender,0,displayFieldCategory);
					break;
				case DisplayFieldCategory.AppointmentBubble:
					AddIfMissing(listDisplayFields,ApptBubblePreAuthFlag,0,displayFieldCategory);
					break;
				case DisplayFieldCategory.TreatmentPlanModule:
					DisplayField displayFieldCatPerc=listDisplayFields.Find(x => x.InternalName==DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR);
					if(displayFieldCatPerc!=null && string.IsNullOrEmpty(displayFieldCatPerc.Description)) {
						displayFieldCatPerc.Description=TreatPlanCatPercStdFeeText;
					}
					break;
			}
		}

		///<summary>The text to show for a display field: its Description when the user set one, the 25.4 wording for a field whose internal name still
		///says "UCR", and otherwise the internal name. Grids that build their headers from InternalName can call this instead.</summary>
		public static string GetColumnText(DisplayField displayField) {
			if(!string.IsNullOrEmpty(displayField.Description)) {
				return displayField.Description;
			}
			if(displayField.Category==DisplayFieldCategory.TreatmentPlanModule
				&& displayField.InternalName==DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR)
			{
				return TreatPlanCatPercStdFeeText;
			}
			return displayField.InternalName;
		}

		private static void AddRange(List<DisplayField> listDisplayFields,List<(string InternalName,int ColumnWidth)> listFields,DisplayFieldCategory displayFieldCategory) {
			for(int i=0;i<listFields.Count;i++) {
				AddIfMissing(listDisplayFields,listFields[i].InternalName,listFields[i].ColumnWidth,displayFieldCategory);
			}
		}

		private static void AddIfMissing(List<DisplayField> listDisplayFields,string internalName,int columnWidth,DisplayFieldCategory displayFieldCategory) {
			if(listDisplayFields.Exists(x => x.InternalName==internalName)) {
				return;
			}
			listDisplayFields.Add(new DisplayField(internalName,columnWidth,displayFieldCategory));
		}
	}
}
