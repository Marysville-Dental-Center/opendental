using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace RegistryTests {
	///<summary>One enum member as Open Dental's published 25.3 database documentation lists it.</summary>
	public class OdEnumMember {
		public string EnumName;
		public string MemberName;
		///<summary>Null when the document prints no number for the member.</summary>
		public int? Number;
		public string Columns;
	}

	///<summary>Reads Data/OdEnums25_3.txt, which holds the enum names and numbers of Open Dental's 25.3 database documentation
	///(https://www.opendental.com/OpenDentalDocumentation25-3.xml). The file holds names and numbers only.</summary>
	public static class OdEnumReference {
		private static List<OdEnumMember> _listMembers;

		public static List<OdEnumMember> GetAll() {
			if(_listMembers!=null) {
				return _listMembers;
			}
			List<OdEnumMember> listMembers=new List<OdEnumMember>();
			foreach(string line in File.ReadAllLines(TestPaths.GetDataFile("OdEnums25_3.txt"))) {
				if(line.Trim()=="" || line.StartsWith("#")) {
					continue;
				}
				string[] arrayParts=line.Split('|');
				OdEnumMember member=new OdEnumMember();
				member.EnumName=arrayParts[0];
				member.MemberName=arrayParts[1];
				if(int.TryParse(arrayParts[2],out int number)) {
					member.Number=number;
				}
				member.Columns=arrayParts.Length>3 ? arrayParts[3] : "";
				listMembers.Add(member);
			}
			_listMembers=listMembers;
			return _listMembers;
		}

		public static List<OdEnumMember> GetEnum(string enumName) {
			return GetAll().FindAll(x => x.EnumName==enumName);
		}

		///<summary>Finds the enum type with this simple name in the business layer or CodeBase. Prefers the OpenDentBusiness namespace when the
		///name is used more than once. Returns null if there is none.</summary>
		public static Type FindCodeEnum(string enumName) {
			if(enumName.Contains(".")) {//Qualified, for example "Pearl.EnumCategoryOD": match the end of the full name.
				string simpleName=enumName.Substring(enumName.LastIndexOf('.')+1);
				Type typeSimple=FindCodeEnum(simpleName);
				if(typeSimple!=null && typeSimple.FullName.Replace('+','.').EndsWith("."+enumName)) {
					return typeSimple;
				}
				return null;
			}
			List<Type> listTypes=new List<Type>();
			foreach(Assembly assembly in new[] { typeof(OpenDentBusiness.Patient).Assembly,typeof(CodeBase.ODException).Assembly }) {
				Type[] arrayTypes;
				try {
					arrayTypes=assembly.GetTypes();
				}
				catch(ReflectionTypeLoadException ex) {
					arrayTypes=ex.Types.Where(x => x!=null).ToArray();
				}
				listTypes.AddRange(arrayTypes.Where(x => x.IsEnum && x.Name==enumName));
			}
			if(listTypes.Count==0) {
				return null;
			}
			return listTypes.FirstOrDefault(x => x.Namespace=="OpenDentBusiness" && !x.IsNested)
				??listTypes.FirstOrDefault(x => x.Namespace=="OpenDentBusiness")
				??listTypes[0];
		}
	}
}
