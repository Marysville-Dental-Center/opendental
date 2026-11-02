// The Open Dental 25.4 and 26.1 preferences: names, value types, seed defaults and PrefInfos.xml entries.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.Seeds26_1;

namespace M202Tests {
	///<summary>Open Dental does not publish preference names, so these are the names the 25.4 and 26.1 feature specs propose. They are this fork's
	///own rows: the office database comes from 25.3.48 and has no 25.4 or 26.1 preference rows to carry over.</summary>
	[TestClass]
	public class Prefs26_1Tests {
		///<summary>Name, value type and default. The defaults keep 24.3 behavior.</summary>
		private static readonly List<(string Name,PrefValueType ValueType,string Default)> _listPrefs=new List<(string,PrefValueType,string)> {
			("IncomeTransfersLimitToPatPayments",PrefValueType.BOOL,"0"),//25.4, M2-PAYW
			("RecurringChargesFailEmailEnabled",PrefValueType.BOOL,"0"),//25.4, M2-RECUR
			("RecurringChargesFailEmailSubject",PrefValueType.NONE,null),//25.4, M2-RECUR (text)
			("RecurringChargesFailEmailBody",PrefValueType.NONE,null),//25.4, M2-RECUR (text)
			("UdsPlusDentalLineCodes",PrefValueType.NONE,""),//25.4, M2-UDS (fork JSON setting)
			("RepeatingChargesAllowBackdating",PrefValueType.BOOL,"1"),//26.1, M2-RECUR
			("ImagingAllowPdfJavascript",PrefValueType.BOOL,"0"),//26.1, M2-IMG
			("AppointmentMakeRecallSearchesOpenings",PrefValueType.BOOL,"1"),//26.1, M2-APPT
			("BillingEmailType",PrefValueType.INT,"0"),//26.1, M2-DUN
			("BillingDefaultServiceDateView",PrefValueType.BOOL,"0"),//26.1, M2-SDV
		};

		///<summary>Preferences shown in the Preferences window, with the control the feature packages are expected to use.</summary>
		private static readonly List<(string Name,string Category,string ControlName)> _listPrefInfos=new List<(string,string,string)> {
			("IncomeTransfersLimitToPatPayments","AccountPayments","checkIncomeTransfersLimitToPatPayments"),
			("RecurringChargesFailEmailEnabled","AccountRecAndRepCharges","checkRecurringChargesFailEmailEnabled"),
			("RepeatingChargesAllowBackdating","AccountRecAndRepCharges","checkRepeatingChargesAllowBackdating"),
			("ImagingAllowPdfJavascript","ImagingGeneral","checkImagingAllowPdfJavascript"),
			("AppointmentMakeRecallSearchesOpenings","ApptGeneral","checkAppointmentMakeRecallSearchesOpenings"),
			("ScheduleProvUnassigned","ApptGeneral","labelScheduleProvUnassigned"),//Existing preference that 26.1 moves into Preferences.
		};

		private static PrefValueType GetValueType(string name) {
			FieldInfo fieldInfo=typeof(PrefName).GetField(name);
			PrefNameAttribute attribute=(PrefNameAttribute)Attribute.GetCustomAttribute(fieldInfo,typeof(PrefNameAttribute));
			return attribute==null ? PrefValueType.NONE : attribute.ValueType;
		}

		[TestMethod]
		public void Names_AreInPrefNameWithTheirValueType() {
			foreach((string name,PrefValueType valueType,string valueDefault) in _listPrefs) {
				Assert.IsTrue(Enum.IsDefined(typeof(PrefName),name),name+" is missing from PrefName.");
				Assert.AreEqual(valueType,GetValueType(name),name);
			}
		}

		[TestMethod]
		public void Seed_HasOneDefaultPerPreference() {
			Assert.AreEqual(_listPrefs.Count,Seed26_1.ListPrefDefaults.Count);
			foreach((string name,PrefValueType valueType,string valueDefault) in _listPrefs) {
				PrefName prefName=(PrefName)Enum.Parse(typeof(PrefName),name);
				List<string> listDefaults=Seed26_1.ListPrefDefaults.Where(x => x.PrefName==prefName).Select(x => x.ValueString).ToList();
				Assert.AreEqual(1,listDefaults.Count,name);
				if(valueDefault!=null) {
					Assert.AreEqual(valueDefault,listDefaults[0],name);
				}
			}
		}

		[TestMethod]
		public void FailEmailTemplate_UsesOnlyTheDocumentedTags() {
			string[] arrayTags=new[] { "[NameF]","[NamePref]","[Amount]","[CardLast4]","[DateCharge]","[OfficeName]","[OfficePhone]","[ClinicName]" };
			foreach(string text in new[] { Seed26_1.RECURRING_FAIL_EMAIL_SUBJECT_DEFAULT,Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT }) {
				Assert.IsFalse(string.IsNullOrWhiteSpace(text));
				foreach(Match match in Regex.Matches(text,@"\[[A-Za-z0-9]+\]")) {
					CollectionAssert.Contains(arrayTags,match.Value,"Unknown tag "+match.Value);
				}
			}
			StringAssert.Contains(Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT,"[Amount]");
			StringAssert.Contains(Seed26_1.RECURRING_FAIL_EMAIL_BODY_DEFAULT,"[CardLast4]");
		}

		[TestMethod]
		public void PreferencesWindow_EntriesHaveHoverInfo() {
			XDocument xDocument=XDocument.Load(TestPaths.GetRepoFile("OpenPmsDental","Resources","PrefInfos.xml"));
			List<XElement> listPrefInfs=xDocument.Root.Elements("PrefInf").ToList();
			HashSet<string> setCategories=new HashSet<string>(Enum.GetNames(typeof(EnumPrefInfoCategory)));
			foreach((string name,string category,string controlName) in _listPrefInfos) {
				List<XElement> listMatches=listPrefInfs.FindAll(x => (string)x.Element("PrefName")==name);
				Assert.AreEqual(1,listMatches.Count,name+" needs exactly one PrefInfos.xml entry.");
				Assert.AreEqual(category,(string)listMatches[0].Element("Category"),name);
				Assert.IsTrue(setCategories.Contains(category),name+" has an unknown category.");
				Assert.AreEqual(controlName,(string)listMatches[0].Element("ControlName"),name);
				Assert.IsFalse(string.IsNullOrWhiteSpace((string)listMatches[0].Element("ControlText")),name+" needs ControlText.");
				Assert.IsFalse(string.IsNullOrWhiteSpace((string)listMatches[0].Element("Details")),name+" needs Details.");
			}
		}
	}
}
