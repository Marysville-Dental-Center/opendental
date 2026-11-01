// Shared helpers for the M2-02 registry tests: repository paths, the published 25.4/26.1 enum changes, and enum lookup by name.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace M202Tests {
	///<summary>Locates the repository and the test data files.</summary>
	public static class TestPaths {
		///<summary>Walks up from the test assembly until it finds OpenDental.sln.</summary>
		public static string GetRepositoryRoot() {
			string dir=Path.GetDirectoryName(typeof(TestPaths).Assembly.Location);
			while(dir!=null) {
				if(File.Exists(Path.Combine(dir,"OpenDental.sln"))) {
					return dir;
				}
				dir=Path.GetDirectoryName(dir);
			}
			throw new InvalidOperationException("Could not find the repository root (OpenDental.sln) above the test assembly.");
		}

		///<summary>A file under the repository root, for example GetRepoFile("OpenDental","Resources","PrefInfos.xml").</summary>
		public static string GetRepoFile(params string[] arrayParts) {
			string path=GetRepositoryRoot();
			for(int i=0;i<arrayParts.Length;i++) {
				path=Path.Combine(path,arrayParts[i]);
			}
			return path;
		}

		///<summary>A data file copied next to the test assembly (Data folder), or the source copy.</summary>
		public static string GetDataFile(string fileName) {
			string path=Path.Combine(Path.GetDirectoryName(typeof(TestPaths).Assembly.Location),"Data",fileName);
			if(File.Exists(path)) {
				return path;
			}
			return GetRepoFile("tests","M202Tests","Data",fileName);
		}
	}

	///<summary>One row of Data/OdEnums26_1Changes.txt.</summary>
	public class OdEnumChange {
		public string EnumName;
		public string PublishedMember;
		public int? Number;
		public string Status;
		public string Source;

		public override string ToString() {
			return EnumName+"."+PublishedMember+"="+(Number?.ToString()??"?")+" ("+Status+")";
		}
	}

	///<summary>Reads Data/OdEnums26_1Changes.txt: the enum values Open Dental's published 25.4 and 26.1 database documents add, rename or renumber.</summary>
	public static class OdEnumChanges {
		public const string STATUS_PUBLISHED="published";
		public const string STATUS_ORDER="order";
		public const string STATUS_OTHER_PACKAGE="other-package";
		public const string STATUS_NOT_ADDED="not-added";

		public static List<OdEnumChange> GetAll() {
			List<OdEnumChange> listChanges=new List<OdEnumChange>();
			foreach(string line in File.ReadAllLines(TestPaths.GetDataFile("OdEnums26_1Changes.txt"))) {
				if(line.Trim()=="" || line.StartsWith("#")) {
					continue;
				}
				string[] arrayParts=line.Split('|');
				OdEnumChange change=new OdEnumChange();
				change.EnumName=arrayParts[0];
				change.PublishedMember=arrayParts[1];
				if(int.TryParse(arrayParts[2],out int number)) {
					change.Number=number;
				}
				change.Status=arrayParts[3];
				change.Source=arrayParts.Length>4 ? arrayParts[4] : "";
				listChanges.Add(change);
			}
			return listChanges;
		}

		///<summary>Finds the enum type with this simple name in the business layer or CodeBase. Prefers the OpenDentBusiness namespace.</summary>
		public static Type FindCodeEnum(string enumName) {
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
