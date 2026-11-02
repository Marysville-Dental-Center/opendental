// The guard of this program's own (fork-only) enum values: unique in their enum, inside their feature's range, and documented exactly
// (docs/port-notes/FORK-ENUMS.md, OpenPmsDentalBusiness.ForkEnums).
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace RegistryTests {
	///<summary>Every fork-only value of every enum: no two names share one, each is in the range of its feature (or is a value released before the
	///ranges, ForkEnums.ListEarlierValues), no other enum has values where fork-only values live, and FORK-ENUMS.md lists exactly these ranges
	///and values.</summary>
	[TestClass]
	public class ForkEnumRangeTests {
		///<summary>One fork-only value: enum, value, member name and feature.</summary>
		private class ForkValue {
			public Type EnumType;
			public long Value;
			public string Name;
			public string Feature;

			public string Key {
				get { return EnumType.Name+"|"+Value+"|"+Name+"|"+Feature; }
			}
		}

		private static List<ForkValue> GetForkValuesInCode() {
			List<ForkValue> listValues=new List<ForkValue>();
			foreach(Type enumType in ForkEnums.ArrayEnums) {
				foreach(string name in Enum.GetNames(enumType)) {
					Enum value=(Enum)Enum.Parse(enumType,name);
					if(!ForkEnums.IsForkOnly(value)) {
						continue;
					}
					listValues.Add(new ForkValue { EnumType=enumType,Value=Convert.ToInt64(value),Name=name,Feature=ForkEnums.GetFeature(value) });
				}
			}
			return listValues;
		}

		[TestMethod]
		public void Ranges_DoNotOverlap_AndStayInTheirKindsRegion() {
			Assert.IsTrue(ForkEnums.ListRanges.Count>0);
			List<string> listProblems=new List<string>();
			foreach(bool isTinyInt in new[] { false,true }) {
				int regionFirst=isTinyInt ? ForkEnums.FIRST_VALUE_TINYINT : ForkEnums.FIRST_VALUE;
				int regionLast=isTinyInt ? ForkEnums.LAST_VALUE_TINYINT : ForkEnums.LAST_VALUE_SMALLINT;
				List<ForkEnumRange> listRanges=ForkEnums.ListRanges.Where(x => x.IsTinyInt==isTinyInt).OrderBy(x => x.First).ToList();
				for(int i=0;i<listRanges.Count;i++) {
					ForkEnumRange range=listRanges[i];
					if(range.First>range.Last || range.First<regionFirst || range.Last>regionLast) {
						listProblems.Add(range.Feature+" "+range.First+"-"+range.Last+" is outside "+regionFirst+"-"+regionLast+".");
					}
					if(i>0 && listRanges[i-1].Last>=range.First) {
						listProblems.Add(listRanges[i-1].Feature+" and "+range.Feature+" overlap.");
					}
				}
				//A feature has at most one range of each kind.
				foreach(IGrouping<string,ForkEnumRange> grouping in listRanges.GroupBy(x => x.Feature).Where(x => x.Count()>1)) {
					listProblems.Add(grouping.Key+" has "+grouping.Count()+" ranges.");
				}
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void ForkValues_AreUniqueInTheirEnum_AndInTheirFeaturesRange() {
			List<string> listProblems=new List<string>();
			foreach(Type enumType in ForkEnums.ArrayEnums) {
				Assert.IsTrue(enumType.IsEnum,enumType.Name);
				Assert.IsFalse(enumType.IsDefined(typeof(FlagsAttribute),false),enumType.Name+" is a flags enum; a fork-only value cannot be a flag.");
				bool isTinyInt=ForkEnums.ArrayTinyIntEnums.Contains(enumType);
				Assert.AreEqual(isTinyInt ? ForkEnums.FIRST_VALUE_TINYINT : ForkEnums.FIRST_VALUE,ForkEnums.GetFirstValue(enumType),enumType.Name);
				List<(string Name,long Value)> listMembers=Enum.GetNames(enumType)
					.Select(x => (x,Convert.ToInt64(Enum.Parse(enumType,x)))).ToList();
				List<(string Name,long Value)> listForkMembers=listMembers.Where(x => x.Value>=ForkEnums.GetFirstValue(enumType)).ToList();
				if(listForkMembers.Count==0) {
					listProblems.Add(enumType.Name+" is in ForkEnums.ArrayEnums but has no fork-only value.");
				}
				//No two names share a fork-only value (Enum.GetNames lists every name, also one that repeats another's number).
				foreach(IGrouping<long,(string Name,long Value)> grouping in listForkMembers.GroupBy(x => x.Value).Where(x => x.Count()>1)) {
					listProblems.Add(enumType.Name+" "+grouping.Key+" is shared by "+string.Join(", ",grouping.Select(x => x.Name))+".");
				}
				foreach((string name,long value) in listForkMembers) {
					string label=enumType.Name+"."+name+" ("+value+")";
					if(isTinyInt && value>ForkEnums.LAST_VALUE_TINYINT) {
						listProblems.Add(label+" does not fit a tinyint(4) column.");
					}
					if(!isTinyInt && value>ForkEnums.LAST_VALUE_SMALLINT) {
						listProblems.Add(label+" does not fit a smallint(6) column.");
					}
					bool isEarlier=ForkEnums.ListEarlierValues.Any(x => x.EnumType==enumType && x.Value==value);
					List<ForkEnumRange> listRanges=ForkEnums.ListRanges.Where(x => x.IsTinyInt==isTinyInt && x.Contains(value)).ToList();
					if(!isEarlier && listRanges.Count!=1) {
						listProblems.Add(label+" is in "+listRanges.Count+" reserved ranges and is not an earlier value; take it from its feature's range.");
					}
					Assert.IsNotNull(ForkEnums.GetFeature((Enum)Enum.Parse(enumType,name)),label);
				}
			}
			//Every earlier value is a fork-only value of an enum that ForkEnums lists.
			foreach(ForkEnumEarlierValue earlierValue in ForkEnums.ListEarlierValues) {
				Assert.IsTrue(ForkEnums.ArrayEnums.Contains(earlierValue.EnumType),earlierValue.EnumType.Name);
				Assert.IsTrue(earlierValue.Value>=ForkEnums.GetFirstValue(earlierValue.EnumType),earlierValue.EnumType.Name+" "+earlierValue.Value);
			}
			Assert.AreEqual(0,listProblems.Count,string.Join("\r\n",listProblems));
		}

		[TestMethod]
		public void ForkValues_AreTheOnesOfTheFeatures() {
			//The values the features chose, as their specifications give them; a change here is a change of a released number.
			Assert.AreEqual(10000,(int)EnumPermType.MaintenanceSetup);
			Assert.AreEqual(10001,(int)EnumPermType.MaintenanceRecord);
			Assert.AreEqual(10050,(int)EnumPermType.InventoryAdjust);
			Assert.AreEqual(10100,(int)EnumPermType.SshHostKeys);
			Assert.AreEqual(127,(int)AlertType.MaintenanceDueSoon);
			Assert.AreEqual(126,(int)AlertType.MaintenanceOverdue);
			Assert.AreEqual(119,(int)AlertType.InventoryLowStock);
			Assert.AreEqual(118,(int)AlertType.InventoryExpiring);
			Assert.AreEqual(127,(int)FormType.FormMaintenanceBoard);
			Assert.AreEqual(119,(int)FormType.FormSupplyInventory);
			Assert.AreEqual(100,(int)DocumentMiscType.ForkReleaseManifest);
			Assert.AreEqual(101,(int)DocumentMiscType.ForkReleaseSegment);
			Assert.AreEqual(114,(int)UserOdFkeyType.PatientFormsView);
			Assert.AreEqual(ForkEnums.FEATURE_MAINTENANCE,ForkEnums.GetFeature(AlertType.MaintenanceOverdue));
			Assert.AreEqual(ForkEnums.FEATURE_INVENTORY,ForkEnums.GetFeature(FormType.FormSupplyInventory));
			Assert.AreEqual(ForkEnums.FEATURE_PATIENT_FORMS_VIEW,ForkEnums.GetFeature(UserOdFkeyType.PatientFormsView));
			Assert.AreEqual(ForkEnums.FEATURE_SSH_HOST_KEYS,ForkEnums.GetFeature(EnumPermType.SshHostKeys));
			Assert.AreEqual(13,GetForkValuesInCode().Count,"A new fork-only value goes into FORK-ENUMS.md and into this test.");
		}

		[TestMethod]
		public void OtherEnums_HaveNoValueFrom10000() {
			//A fork-only value from 10000 up in an enum ForkEnums does not list would escape the checks above. The enums stored in the database are
			//the business layer's; flags enums hold large bit values. (CodeBase's Windows API enums hold window style bits, and nothing stores them.)
			List<string> listProblems=new List<string>();
			foreach(Assembly assembly in new[] { typeof(ForkEnums).Assembly }) {
				Type[] arrayTypes;
				try {
					arrayTypes=assembly.GetTypes();
				}
				catch(ReflectionTypeLoadException ex) {
					arrayTypes=ex.Types.Where(x => x!=null).ToArray();
				}
				foreach(Type type in arrayTypes.Where(x => x.IsEnum && !ForkEnums.ArrayEnums.Contains(x) && !x.IsDefined(typeof(FlagsAttribute),false))) {
					List<string> listHigh=Enum.GetNames(type).Where(x => Convert.ToDecimal(Enum.Parse(type,x))>=ForkEnums.FIRST_VALUE).ToList();
					if(listHigh.Count>0) {
						listProblems.Add(type.FullName+": "+string.Join(", ",listHigh));
					}
				}
			}
			Assert.AreEqual(0,listProblems.Count,"Values from 10000 up in an enum that ForkEnums does not list:\r\n"+string.Join("\r\n",listProblems));
		}

		///<summary>The rows of the first Markdown table after the line that starts with the heading text (or of the first table of the file when
		///heading is null), each as its trimmed cells, without the header and separator rows.</summary>
		private static List<string[]> ReadTable(string text,string heading,string firstHeaderCell) {
			string[] arrayLines=text.Replace("\r\n","\n").Split('\n');
			int index=0;
			if(heading!=null) {
				index=Array.FindIndex(arrayLines,x => x.StartsWith(heading));
				Assert.IsTrue(index>=0,"FORK-ENUMS.md has no heading "+heading);
			}
			index=Array.FindIndex(arrayLines,index,x => x.StartsWith("|") && x.Split('|')[1].Trim()==firstHeaderCell);
			Assert.IsTrue(index>=0,"FORK-ENUMS.md has no table that starts with "+firstHeaderCell);
			List<string[]> listRows=new List<string[]>();
			for(int i=index+2;i<arrayLines.Length && arrayLines[i].StartsWith("|");i++) {
				listRows.Add(arrayLines[i].Trim().Trim('|').Split('|').Select(x => x.Trim()).ToArray());
			}
			return listRows;
		}

		private static List<string> ParseRanges(List<string[]> listRows,bool isTinyInt,out string free) {
			List<string> listRanges=new List<string>();
			free=null;
			foreach(string[] arrayCells in listRows) {
				if(arrayCells[1].StartsWith("Free")) {
					free=arrayCells[0];
					continue;
				}
				Match match=Regex.Match(arrayCells[0],@"^(\d+)-(\d+)$");
				Assert.IsTrue(match.Success,"Not a range: "+arrayCells[0]);
				int a=int.Parse(match.Groups[1].Value);
				int b=int.Parse(match.Groups[2].Value);
				listRanges.Add(isTinyInt+"|"+Math.Min(a,b)+"|"+Math.Max(a,b)+"|"+arrayCells[1]);
			}
			return listRanges;
		}

		[TestMethod]
		public void Document_ListsExactlyTheRangesAndTheValuesOfTheCode() {
			string text=File.ReadAllText(TestPaths.GetRepoFile("docs","port-notes","FORK-ENUMS.md"));
			List<string> listRangesDoc=ParseRanges(ReadTable(text,null,"Range"),false,out string freeWide);
			listRangesDoc.AddRange(ParseRanges(ReadTable(text,null,"Range (tinyint enums)"),true,out string freeTiny));
			List<string> listRangesCode=ForkEnums.ListRanges.Select(x => x.IsTinyInt+"|"+x.First+"|"+x.Last+"|"+x.Feature).ToList();
			CollectionAssert.AreEquivalent(listRangesCode,listRangesDoc,"The range tables of FORK-ENUMS.md and ForkEnums.ListRanges differ.");
			//The free part starts after the last wide range and ends below the lowest tinyint range.
			Assert.AreEqual((ForkEnums.ListRanges.Where(x => !x.IsTinyInt).Max(x => x.Last)+1)+"-",freeWide);
			Assert.AreEqual((ForkEnums.ListRanges.Where(x => x.IsTinyInt).Min(x => x.First)-1)+"-"+ForkEnums.FIRST_VALUE_TINYINT,freeTiny);
			List<string> listValuesDoc=ReadTable(text,"## Every fork-only value","Enum").Select(x => x[0]+"|"+x[1]+"|"+x[2]+"|"+x[3]).ToList();
			List<string> listValuesCode=GetForkValuesInCode().Select(x => x.Key).ToList();
			CollectionAssert.AreEquivalent(listValuesCode,listValuesDoc,"FORK-ENUMS.md, \"Every fork-only value\", must list exactly the code's values:\r\n"
				+string.Join("\r\n",listValuesCode));
			//Every enum with fork-only values is named in the document's tinyint section or is EnumPermType.
			foreach(Type enumType in ForkEnums.ArrayTinyIntEnums) {
				StringAssert.Contains(text,"`"+enumType.Name+"`",enumType.Name);
			}
		}
	}
}
