using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenDentBusiness {
	///<summary>The procedure code system a default list or a code import is for: CDT (D codes, United States) or CDA (Canada). Not stored in the
	///database.</summary>
	public enum EnumDentalCodeSystem {
		///<summary>0 - D codes.</summary>
		CDT,
		///<summary>1 - Canadian codes.</summary>
		CDA,
	}

	///<summary>The program's default code groups, shared by Procedure Code Tools (Reset Code Groups, Open Dental 25.3.54 and 25.4) and anything else
	///that needs the default list. The list is the frequency limitation set that Open Dental 22.x created from the old InsBen*Codes preferences
	///(ConvertDatabases8, ConvertFrequencyLimitationPreferences), in the same order and with the same fixed types, plus the age-limit flags that
	///the later Fluoride and Sealant conversion set. Only procedure code numbers are listed here; no code descriptions are shipped.</summary>
	public static class CodeGroupDefaults {
		///<summary>One default code group: its name, codes for each code system, fixed type and grid flags.</summary>
		private class CodeGroupDefault {
			public string GroupName;
			public string ProcCodesCdt;
			public string ProcCodesCda;
			public EnumCodeGroupFixed CodeGroupFixed;
			public bool ShowInAgeLimit;

			public CodeGroupDefault(string groupName,string procCodesCdt,string procCodesCda,EnumCodeGroupFixed codeGroupFixed,bool showInAgeLimit=false) {
				GroupName=groupName;
				ProcCodesCdt=procCodesCdt;
				ProcCodesCda=procCodesCda;
				CodeGroupFixed=codeGroupFixed;
				ShowInAgeLimit=showInAgeLimit;
			}
		}

		///<summary>In ItemOrder. The CDT lists are the defaults of the 24.3 conversion (GetDefaultProcCodesForPrefName). For CDA, Open Dental's
		///conversion only knew the Fluoride (12111) and Sealant (13401) codes; the other CDA lists start empty and the office fills them in Setup,
		///Code Groups, except that Reset Code Groups keeps the codes of the existing group an empty default replaces (INFERRED, see
		///docs/port-notes/B-PROC.md).</summary>
		private static readonly List<CodeGroupDefault> _listDefaults=new List<CodeGroupDefault> {
			new CodeGroupDefault("BW","D0272,D0274","",EnumCodeGroupFixed.BW),
			new CodeGroupDefault("Exam","D0120,D0150","",EnumCodeGroupFixed.Exam),
			new CodeGroupDefault("Pano/FMX","D0210,D0330","",EnumCodeGroupFixed.PanoFMX),
			new CodeGroupDefault("Cancer Screening","D0431","",EnumCodeGroupFixed.None),
			new CodeGroupDefault("Prophy","D1110,D1120","",EnumCodeGroupFixed.Prophy),
			new CodeGroupDefault("Fluoride","D1206,D1208","12111",EnumCodeGroupFixed.Fluoride,showInAgeLimit:true),
			new CodeGroupDefault("Sealant","D1351","13401",EnumCodeGroupFixed.Sealant,showInAgeLimit:true),
			new CodeGroupDefault("Crown","D2740,D2750,D2751,D2752,D2780,D2781,D2782,D2783,D2790,D2791,D2792,D2794","",EnumCodeGroupFixed.None),
			new CodeGroupDefault("SRP","D4341,D4342","",EnumCodeGroupFixed.SRP),
			new CodeGroupDefault("Full Debridement","D4355","",EnumCodeGroupFixed.FMDebride),
			new CodeGroupDefault("Perio Maint","D4910","",EnumCodeGroupFixed.Perio),
			new CodeGroupDefault("Dentures","D5110,D5120,D5130,D5140,D5211,D5212,D5213,D5214,D5221,D5222,D5223,D5224,D5225,D5226","",
				EnumCodeGroupFixed.None),
			new CodeGroupDefault("Implants","D6010","",EnumCodeGroupFixed.None),
		};

		///<summary>New (not inserted) code groups for the code system, ItemOrder 0 and up. Every default group is a row of the frequency limitations
		///grid, as the conversion made them; Fluoride and Sealant also show in the Age Limitations grid. None is retired or shows in Other Benefits
		///(the 25.3 conversion default).</summary>
		public static List<CodeGroup> GetDefaults(EnumDentalCodeSystem codeSystem) {
			List<CodeGroup> listCodeGroups=new List<CodeGroup>();
			for(int i=0;i<_listDefaults.Count;i++) {
				CodeGroup codeGroup=new CodeGroup();
				codeGroup.GroupName=_listDefaults[i].GroupName;
				codeGroup.ProcCodes=codeSystem==EnumDentalCodeSystem.CDA ? _listDefaults[i].ProcCodesCda : _listDefaults[i].ProcCodesCdt;
				codeGroup.ItemOrder=i;
				codeGroup.CodeGroupFixed=_listDefaults[i].CodeGroupFixed;
				codeGroup.IsHidden=false;
				codeGroup.ShowInAgeLimit=_listDefaults[i].ShowInAgeLimit;
				codeGroup.ShowInFrequency=true;
				codeGroup.ShowInOther=false;
				listCodeGroups.Add(codeGroup);
			}
			return listCodeGroups;
		}

		///<summary>The code system this computer uses by default: CDA in Canada (en-CA, fr-CA), otherwise CDT.</summary>
		public static EnumDentalCodeSystem GetCodeSystemForCulture(string cultureName) {
			if(cultureName!=null && cultureName.EndsWith("CA")) {
				return EnumDentalCodeSystem.CDA;
			}
			return EnumDentalCodeSystem.CDT;
		}
	}
}
