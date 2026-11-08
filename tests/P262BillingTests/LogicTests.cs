// The parts of the 26.2 billing and statements overhaul (package P262-BILLING) that need no database: the stored form of the electronic billing
// settings (the same text Billing Defaults wrote before 26.2), the choices of the Statement Options window, the per-clinic settings object, and
// the rows Billing Options' Save Defaults writes for the selected clinic.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;

namespace P262BillingTests {
	[TestClass]
	public class EbillSettingsTests {
		[TestMethod]
		public void CreditCardChoices_WrittenAsBillingDefaultsWroteThem() {
			Assert.AreEqual("",EbillSettings.FormatCreditCardChoices(false,false,false,false));
			Assert.AreEqual("MC",EbillSettings.FormatCreditCardChoices(true,false,false,false));
			Assert.AreEqual("V,A",EbillSettings.FormatCreditCardChoices(false,true,false,true));
			Assert.AreEqual("MC,V,D,A",EbillSettings.FormatCreditCardChoices(true,true,true,true));
			Assert.AreEqual("D",EbillSettings.FormatCreditCardChoices(false,false,true,false));
		}

		[TestMethod]
		public void CreditCardChoices_ReadAsBillingDefaultsReadThem() {
			//Every combination round-trips.
			for(int i=0;i<16;i++) {
				bool isMasterCard=(i&1)!=0;
				bool isVisa=(i&2)!=0;
				bool isDiscover=(i&4)!=0;
				bool isAmEx=(i&8)!=0;
				string stored=EbillSettings.FormatCreditCardChoices(isMasterCard,isVisa,isDiscover,isAmEx);
				EbillSettings.ParseCreditCardChoices(stored,out bool isMasterCardRead,out bool isVisaRead,out bool isDiscoverRead,out bool isAmExRead);
				Assert.AreEqual(isMasterCard,isMasterCardRead,stored);
				Assert.AreEqual(isVisa,isVisaRead,stored);
				Assert.AreEqual(isDiscover,isDiscoverRead,stored);
				Assert.AreEqual(isAmEx,isAmExRead,stored);
			}
			//A stored value is read by its letters anywhere, as before 26.2 (string.Contains), and null is nothing.
			EbillSettings.ParseCreditCardChoices("A,MC",out bool isMc,out bool isV,out bool isD,out bool isA);
			Assert.IsTrue(isMc && !isV && !isD && isA);
			EbillSettings.ParseCreditCardChoices(null,out isMc,out isV,out isD,out isA);
			Assert.IsFalse(isMc || isV || isD || isA);
		}

		[TestMethod]
		public void Methods_InTheOrderOfTheStoredValue() {
			Assert.AreEqual(5,EbillSettings.ArrayMethodNames.Length);
			Assert.AreEqual(0,(int)BillingUseElectronicEnum.None);
			Assert.AreEqual("No electronic billing",EbillSettings.ArrayMethodNames[(int)BillingUseElectronicEnum.None]);
			Assert.AreEqual("DentalXChange",EbillSettings.ArrayMethodNames[1]);
			Assert.AreEqual("Output to file",EbillSettings.ArrayMethodNames[2]);
			Assert.AreEqual("ClaimX / ExtraDent",EbillSettings.ArrayMethodNames[3]);
			Assert.AreEqual("EDS",EbillSettings.ArrayMethodNames[4]);
			Assert.AreEqual(4,Enum.GetValues(typeof(BillingUseElectronicEnum)).Cast<int>().Max(),"One list entry per stored value.");
			foreach(BillingUseElectronicEnum method in Enum.GetValues(typeof(BillingUseElectronicEnum))) {
				Assert.AreEqual(CodeBase.EnumTools.GetAttributeOrDefault<System.ComponentModel.DescriptionAttribute>(method).Description,
					EbillSettings.ArrayMethodNames[(int)method],method.ToString());
			}
		}

		[TestMethod]
		public void OutputPaths_OnePreferencePerMethod() {
			Assert.IsNull(EbillSettings.GetOutputPathPrefName(0));
			Assert.AreEqual(PrefName.BillingElectStmtUploadURL,EbillSettings.GetOutputPathPrefName(1));
			Assert.AreEqual(PrefName.BillingElectStmtOutputPathPos,EbillSettings.GetOutputPathPrefName(2));
			Assert.AreEqual(PrefName.BillingElectStmtOutputPathClaimX,EbillSettings.GetOutputPathPrefName(3));
			Assert.AreEqual(PrefName.BillingElectStmtOutputPathEds,EbillSettings.GetOutputPathPrefName(4));
			Assert.IsNull(EbillSettings.GetOutputPathPrefName(5));
			Assert.IsNull(EbillSettings.GetOutputPathPrefName(-1));
			//The folder methods need a folder; DentalXChange has a URL (blank for its default) and none is required.
			CollectionAssert.AreEqual(new[] { false,false,true,true,true },Enumerable.Range(0,5).Select(x => EbillSettings.IsOutputPathRequired(x)).ToArray());
			CollectionAssert.AreEqual(new[] { false,true,false,true,false },Enumerable.Range(0,5).Select(x => EbillSettings.CanCreatePdf(x)).ToArray());
			Assert.AreEqual("https://claimconnect.dentalxchange.com/dci/upload.svl",EbillSettings.DXC_URL_DEFAULT);
		}

		[TestMethod]
		public void Preferences_NoneIsAlsoABillingDefaultsMessage() {
			Assert.AreEqual(11,EbillSettings.ListPrefNames.Count);
			Assert.AreEqual(EbillSettings.ListPrefNames.Count,EbillSettings.ListPrefNames.Distinct().Count());
			Assert.IsFalse(EbillSettings.ListPrefNames.Any(x => BillingDefaultsByClinic.IsPerClinic(x)));
			for(int i=1;i<5;i++) {
				CollectionAssert.Contains(EbillSettings.ListPrefNames,EbillSettings.GetOutputPathPrefName(i).Value);
			}
		}
	}

	[TestClass]
	public class StatementChoicesTests {
		[TestMethod]
		public void EveryChoice_HasATitleAndADescription_InTheMenusOrder() {
			List<StatementChoice> listChoices=StatementChoices.GetAllInOrder();
			CollectionAssert.AreEquivalent(Enum.GetValues(typeof(StatementChoice)).Cast<StatementChoice>().ToList(),listChoices);
			Assert.AreEqual(StatementChoice.Statement,listChoices[0],"The default statement first.");
			foreach(StatementChoice statementChoice in listChoices) {
				Assert.IsFalse(string.IsNullOrWhiteSpace(StatementChoices.GetTitle(statementChoice)),statementChoice.ToString());
				string description=StatementChoices.GetDescription(statementChoice);
				Assert.IsFalse(string.IsNullOrWhiteSpace(description),statementChoice.ToString());
				Assert.IsTrue(description.EndsWith("."),statementChoice.ToString());
			}
			//The titles are the texts of the former dropdown entries.
			CollectionAssert.AreEqual(new[] { "Statement","Walkout","Email","Receipt","Invoice","Limited","Limited (Custom)","More Options",
				"Service Date View Window" },listChoices.Select(x => StatementChoices.GetTitle(x)).ToArray());
			Assert.AreEqual(listChoices.Count,listChoices.Select(x => StatementChoices.GetDescription(x)).Distinct().Count());
		}
	}

	[TestClass]
	public class BillingDefaultsSettingsTests {
		[TestMethod]
		public void Settings_CopyAndCompare() {
			BillingDefaultsSettings settings=new BillingDefaultsSettings { ClinicNum=3,GeneralMessage="a",EmailSubject="b",EmailBody="c",
				EmailType=EmailType.Html,IncludeAutograph=true,InvoiceNote="d" };
			BillingDefaultsSettings copy=settings.Copy();
			Assert.IsTrue(copy.IsSameAs(settings));
			copy.InvoiceNote="e";
			Assert.IsFalse(copy.IsSameAs(settings));
			Assert.AreEqual("d",settings.InvoiceNote,"A copy, not the same object.");
			copy=settings.Copy();
			copy.UsePracticeDefaults=true;
			Assert.IsFalse(copy.IsSameAs(settings));
			copy=settings.Copy();
			copy.ClinicNum=4;
			Assert.IsFalse(copy.IsSameAs(settings));
			Assert.IsFalse(settings.IsSameAs(null));
			//Null and empty text are the same setting.
			copy=settings.Copy();
			copy.GeneralMessage=null;
			settings.GeneralMessage="";
			Assert.IsTrue(copy.IsSameAs(settings));
		}

		[TestMethod]
		public void Values_InTheOrderOfThePreferences_AsThePreferenceRowsHoldThem() {
			BillingDefaultsSettings settings=new BillingDefaultsSettings { GeneralMessage="a",EmailSubject="b",EmailBody="c",
				EmailType=EmailType.RawHtml,IncludeAutograph=true,InvoiceNote=null };
			List<KeyValuePair<PrefName,string>> listValues=BillingDefaultsByClinic.GetValues(settings);
			CollectionAssert.AreEqual(BillingDefaultsByClinic.ListPrefNames,listValues.Select(x => x.Key).ToList());
			CollectionAssert.AreEqual(new[] { "a","b","c","2","1","" },listValues.Select(x => x.Value).ToArray());
			settings.IncludeAutograph=false;
			settings.EmailType=EmailType.Regular;
			listValues=BillingDefaultsByClinic.GetValues(settings);
			Assert.AreEqual("0",listValues.First(x => x.Key==PrefName.BillingEmailType).Value);
			Assert.AreEqual("0",listValues.First(x => x.Key==PrefName.BillingEmailIncludeAutograph).Value);
		}

		[TestMethod]
		public void PerClinicPreferences_AreTheSixMessages() {
			CollectionAssert.AreEqual(new[] { PrefName.BillingDefaultsNote,PrefName.BillingEmailSubject,PrefName.BillingEmailBodyText,
				PrefName.BillingEmailType,PrefName.BillingEmailIncludeAutograph,PrefName.BillingDefaultsInvoiceNote },
				BillingDefaultsByClinic.ListPrefNames.ToArray());
			//The statement defaults of the Statements box stay practice-wide.
			foreach(PrefName prefName in new[] { PrefName.BillingDefaultsLastDays,PrefName.BillingDefaultsIntermingle,PrefName.BillingDefaultsSinglePatient,
				PrefName.BillingShowTransSinceBalZero })
			{
				Assert.IsFalse(BillingDefaultsByClinic.IsPerClinic(prefName),prefName.ToString());
			}
		}
	}

	///<summary>Billing Options' filter defaults per clinic beside Billing Defaults' General Message row (review fix of P262-BILLING): which clinic has
	///saved filter defaults, and the rows its Save Defaults writes for the selected clinic.</summary>
	[TestClass]
	public class BillingOptionsClinicDefaultsTests {
		private const long CLINIC_NUM=7;
		private const string PRACTICE_NOTE="Practice general message";

		private static List<KeyValuePair<PrefName,string>> GetValues(string generalMessage) {
			return BillingOptionsClinicDefaults.ListPrefNames
				.Select(x => new KeyValuePair<PrefName,string>(x,x==PrefName.BillingDefaultsNote ? generalMessage : "value of "+x))
				.ToList();
		}

		[TestMethod]
		public void ListPrefNames_TheFourteenBillingOptionsSavedBefore() {
			CollectionAssert.AreEqual(new[] { PrefName.BillingIncludeChanged,PrefName.BillingSelectBillingTypes,PrefName.BillingAgeOfAccount,
				PrefName.BillingDaysExcludeInsPending,PrefName.BillingExcludeBadAddresses,PrefName.BillingExcludeInactive,PrefName.BillingExcludeNegative,
				PrefName.BillingExcludeInsPending,PrefName.BillingExcludeIfUnsentProcs,PrefName.BillingExcludeLessThan,PrefName.BillingIgnoreInPerson,
				PrefName.BillingShowTransSinceBalZero,PrefName.BillingDefaultsNote,PrefName.BillingSelectInsFilingCodes },
				BillingOptionsClinicDefaults.ListPrefNames.ToArray());
			//The General Message is the only one Billing Defaults saves per clinic too.
			CollectionAssert.AreEqual(new[] { PrefName.BillingDefaultsNote },
				BillingOptionsClinicDefaults.ListPrefNames.Where(x => BillingDefaultsByClinic.IsPerClinic(x)).ToArray());
		}

		[TestMethod]
		public void HasSavedFilterDefaults_NotForTheGeneralMessageAlone() {
			Assert.IsFalse(BillingOptionsClinicDefaults.HasSavedFilterDefaults(null));
			Assert.IsFalse(BillingOptionsClinicDefaults.HasSavedFilterDefaults(new List<ClinicPref>()));
			List<ClinicPref> listClinicPrefs=new List<ClinicPref>() { new ClinicPref(CLINIC_NUM,PrefName.BillingDefaultsNote,"Clinic message") };
			Assert.IsFalse(BillingOptionsClinicDefaults.HasSavedFilterDefaults(listClinicPrefs),"Only the General Message, saved by Billing Defaults.");
			listClinicPrefs.Add(new ClinicPref(CLINIC_NUM,PrefName.BillingEmailSubject,"Clinic subject"));
			Assert.IsFalse(BillingOptionsClinicDefaults.HasSavedFilterDefaults(listClinicPrefs),"Billing Defaults' other messages are no filter.");
			listClinicPrefs.Add(new ClinicPref(CLINIC_NUM,PrefName.BillingSelectInsFilingCodes,""));
			Assert.IsTrue(BillingOptionsClinicDefaults.HasSavedFilterDefaults(listClinicPrefs));
			Assert.IsTrue(BillingOptionsClinicDefaults.HasSavedFilterDefaults(new List<ClinicPref>() {
				new ClinicPref(CLINIC_NUM,PrefName.BillingIncludeChanged,true) }),"A filter row without the General Message.");
		}

		[TestMethod]
		public void RowsToSave_NewClinic_EveryValueAsANewRow() {
			List<ClinicPref> listClinicPrefs=BillingOptionsClinicDefaults.GetRowsToSave(CLINIC_NUM,new List<ClinicPref>(),GetValues("Own message"),PRACTICE_NOTE);
			CollectionAssert.AreEquivalent(BillingOptionsClinicDefaults.ListPrefNames,listClinicPrefs.Select(x => x.PrefName).ToList());
			Assert.IsTrue(listClinicPrefs.All(x => x.ClinicNum==CLINIC_NUM && x.ClinicPrefNum==0));
			Assert.AreEqual("Own message",listClinicPrefs.First(x => x.PrefName==PrefName.BillingDefaultsNote).ValueString);
			Assert.AreEqual("value of BillingAgeOfAccount",listClinicPrefs.First(x => x.PrefName==PrefName.BillingAgeOfAccount).ValueString);
		}

		[TestMethod]
		public void RowsToSave_ThePracticeMessage_GetsNoRowOfItsOwn() {
			List<ClinicPref> listClinicPrefs=BillingOptionsClinicDefaults.GetRowsToSave(CLINIC_NUM,null,GetValues(PRACTICE_NOTE),PRACTICE_NOTE);
			Assert.AreEqual(13,listClinicPrefs.Count);
			Assert.IsFalse(listClinicPrefs.Any(x => x.PrefName==PrefName.BillingDefaultsNote));
		}

		[TestMethod]
		public void RowsToSave_ExistingRowsUpdatedInPlace_OthersOnlyAdded() {
			List<ClinicPref> listClinicPrefsOld=new List<ClinicPref>() {
				new ClinicPref(CLINIC_NUM,PrefName.BillingDefaultsNote,"Billing Defaults message") { ClinicPrefNum=31 },
				new ClinicPref(CLINIC_NUM,PrefName.BillingIncludeChanged,false) { ClinicPrefNum=32 },
				new ClinicPref(CLINIC_NUM+1,PrefName.BillingAgeOfAccount,"90") { ClinicPrefNum=33 },//Another clinic's row: left out.
				new ClinicPref(CLINIC_NUM,PrefName.BillingEmailSubject,"Clinic subject") { ClinicPrefNum=34 },//Not Billing Options': left out.
			};
			List<KeyValuePair<PrefName,string>> listValues=GetValues(PRACTICE_NOTE);
			listValues.Add(new KeyValuePair<PrefName,string>(PrefName.BillingEmailSubject,"Not saved here"));
			List<ClinicPref> listClinicPrefs=BillingOptionsClinicDefaults.GetRowsToSave(CLINIC_NUM,listClinicPrefsOld,listValues,PRACTICE_NOTE);
			Assert.AreEqual(14,listClinicPrefs.Count);
			Assert.IsTrue(listClinicPrefs.All(x => x.ClinicNum==CLINIC_NUM));
			Assert.IsFalse(listClinicPrefs.Any(x => x.PrefName==PrefName.BillingEmailSubject));
			ClinicPref clinicPrefNote=listClinicPrefs.Single(x => x.PrefName==PrefName.BillingDefaultsNote);
			Assert.AreEqual(31,clinicPrefNote.ClinicPrefNum,"The clinic's own row is updated, even to the practice text.");
			Assert.AreEqual(PRACTICE_NOTE,clinicPrefNote.ValueString);
			ClinicPref clinicPrefIncludeChanged=listClinicPrefs.Single(x => x.PrefName==PrefName.BillingIncludeChanged);
			Assert.AreEqual(32,clinicPrefIncludeChanged.ClinicPrefNum);
			Assert.AreEqual("value of BillingIncludeChanged",clinicPrefIncludeChanged.ValueString);
			Assert.AreEqual(12,listClinicPrefs.Count(x => x.ClinicPrefNum==0));
			//The rows passed in are copied, not changed.
			Assert.AreEqual("Billing Defaults message",listClinicPrefsOld[0].ValueString);
			Assert.AreEqual("0",listClinicPrefsOld[1].ValueString);
		}
	}
}
