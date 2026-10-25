// String review for the 25.4 wording: the shown text in the files M2-TERM owns says "standard provider fee" (or "standard fee" in narrow report
// columns) instead of "UCR", while preference names, column names, enum members and control names stay as they were.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using CodeBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace M2TERMTests {
	[TestClass]
	public class TerminologyStringTests {
		[TestMethod]
		public void EachChange_OldTextGone_NewTextThere() {
			List<string> listFailures=new List<string>();
			foreach(WordingChange change in TermSources.ListChanges) {
				string source=TestPaths.ReadRepoFile(change.File);
				int countOld=change.CountIn(source,change.OldText);
				int countNew=change.CountIn(source,change.NewText);
				//A renamed report column keeps the old header as its export key, in the statement right after it.
				int countOldAsExportKey=TermSources.GetReportColumns(source)
					.Count(x => x.HasExportKeyStatement && x.Header==change.NewText && x.ExportKey==change.OldText);
				if(countOldAsExportKey!=change.ExportKeyCount) {
					listFailures.Add(change+": the old text is the export key of the renamed column "+countOldAsExportKey+" time(s), expected "
						+change.ExportKeyCount+".");
				}
				if(countOld!=countOldAsExportKey) {
					listFailures.Add(change+": the old text is still there "+(countOld-countOldAsExportKey)+" time(s) besides the export keys.");
				}
				if(countNew!=change.Count) {
					listFailures.Add(change+": the new text is there "+countNew+" time(s), expected "+change.Count+".");
				}
			}
			Assert.AreEqual(0,listFailures.Count,string.Join(Environment.NewLine,listFailures));
		}

		[TestMethod]
		public void OwnedFiles_ShowNoUcr() {
			List<string> listFailures=new List<string>();
			foreach(string file in TermSources.ListOwnedSourceFiles) {
				foreach(string literal in TermSources.GetShownLiterals(TestPaths.ReadRepoFile(file))) {
					if(TermSources.RegexUcrWord.IsMatch(literal)) {
						listFailures.Add(file+": \""+literal+"\"");
					}
				}
			}
			Assert.AreEqual(0,listFailures.Count,"Shown text that still says UCR:"+Environment.NewLine+string.Join(Environment.NewLine,listFailures));
		}

		[TestMethod]
		public void ShownContexts_FindTheChangedLiterals() {
			//Guards the scan above: if the context patterns stopped matching, OwnedFiles_ShowNoUcr would pass on nothing.
			List<string> listShown=TermSources.ListOwnedSourceFiles.SelectMany(x => TermSources.GetShownLiterals(TestPaths.ReadRepoFile(x))).ToList();
			foreach(WordingChange change in TermSources.ListChanges) {
				Assert.IsTrue(listShown.Any(x => x==change.NewText || (change.IsTail && x.EndsWith(change.NewText))),
					"The shown-text scan does not see: "+change);
			}
			Assert.IsTrue(TermSources.RegexUcrWord.IsMatch("Use UCR fee"));
			Assert.IsTrue(TermSources.RegexUcrWord.IsMatch("Cat% UCR"));
			Assert.IsFalse(TermSources.RegexUcrWord.IsMatch("checkInsPlanExclusionsUseUCR"));
			Assert.IsFalse(TermSources.RegexUcrWord.IsMatch("textUcrFeePercent"));
		}

		[TestMethod]
		public void ExclusionRule_DescriptionsAndNumbers() {
			Assert.AreEqual("Practice Default",ODPrimitiveExtensions.GetDescription(ExclusionRule.PracticeDefault));
			Assert.AreEqual("Do Nothing",ODPrimitiveExtensions.GetDescription(ExclusionRule.DoNothing));
			Assert.AreEqual("Use Standard Provider Fee",ODPrimitiveExtensions.GetDescription(ExclusionRule.UseUcrFee));
			//Stored numbers and the member name do not change.
			Assert.AreEqual(0,(int)ExclusionRule.PracticeDefault);
			Assert.AreEqual(1,(int)ExclusionRule.DoNothing);
			Assert.AreEqual(2,(int)ExclusionRule.UseUcrFee);
			Assert.AreEqual("UseUcrFee",Enum.GetName(typeof(ExclusionRule),2));
			Assert.AreEqual(3,Enum.GetValues(typeof(ExclusionRule)).Length);
		}

		[TestMethod]
		public void BlueBookProviderFeeRule_NeverSaysUcr() {
			Assert.AreEqual(5,(int)InsBlueBookRuleType.ProviderFee);
			Assert.IsFalse(TermSources.RegexUcrWord.IsMatch(ODPrimitiveExtensions.GetDescription(InsBlueBookRuleType.ProviderFee)));
		}

		[TestMethod]
		public void InternalNames_Unchanged() {
			//Preference names: office databases hold rows with these names.
			foreach(string prefName in new[] { "InsDefaultShowUCRonClaims","InsPlanUseUcrFeeForExclusions","InsPpoAlwaysUseUcrFee","InsBlueBookUcrFeePercent" }) {
				Assert.IsTrue(Enum.IsDefined(typeof(PrefName),prefName),"PrefName."+prefName+" must stay.");
			}
			//Column names.
			Assert.IsNotNull(typeof(InsPlan).GetField("ClaimsUseUCR",BindingFlags.Public|BindingFlags.Instance),"insplan.ClaimsUseUCR must stay.");
			Assert.IsNotNull(typeof(ProcTP).GetField("CatPercUCR",BindingFlags.Public|BindingFlags.Instance),"proctp.CatPercUCR must stay.");
			//Display field internal name: saved displayfield rows use it.
			Assert.AreEqual("Cat% UCR",DisplayFields.InternalNames.TreatmentPlanModule.CatPercUCR);
			//Control names: PrefInfos.xml finds its hover help by control name.
			string designerFamily=TestPaths.ReadRepoFile(TermSources.FAMILY_INSURANCE_DESIGNER);
			Assert.AreEqual(1,TermSources.CountLiteral(designerFamily,"checkInsPlanExclusionsUseUCR"));
			Assert.AreEqual(1,TermSources.CountLiteral(designerFamily,"checkInsDefaultShowUCRonClaims"));
			Assert.AreEqual(1,TermSources.CountLiteral(TestPaths.ReadRepoFile(TermSources.ACCOUNT_CLAIM_SEND_DESIGNER),"checkPpoUseUcr"));
			Assert.AreEqual(1,TermSources.CountLiteral(TestPaths.ReadRepoFile(TermSources.BLUE_BOOK_RULES_DESIGNER),"labelUcrFeePercent"));
		}

		[TestMethod]
		public void PrefInfos_ControlTextMatchesTheControls_AndHelpSaysNoUcr() {
			XDocument xDocument=XDocument.Load(TestPaths.GetRepoFile(TermSources.PREF_INFOS));
			List<XElement> listPrefInfs=xDocument.Root.Elements("PrefInf").ToList();
			List<(string Category,string ControlName,string Designer)> listControls=new List<(string,string,string)> {
				("AccountClaimSend","checkPpoUseUcr",TermSources.ACCOUNT_CLAIM_SEND_DESIGNER),
				("FamilyInsurance","checkInsPlanExclusionsUseUCR",TermSources.FAMILY_INSURANCE_DESIGNER),
				("FamilyInsurance","checkInsDefaultShowUCRonClaims",TermSources.FAMILY_INSURANCE_DESIGNER),
			};
			foreach((string category,string controlName,string designer) in listControls) {
				List<XElement> listMatches=listPrefInfs.FindAll(x => (string)x.Element("Category")==category && (string)x.Element("ControlName")==controlName);
				Assert.AreEqual(1,listMatches.Count,controlName+" needs exactly one PrefInfos.xml entry.");
				string textControl=DesignerControl.Read(TestPaths.ReadRepoFile(designer),controlName).Text;
				Assert.AreEqual(textControl,(string)listMatches[0].Element("ControlText"),controlName+": PrefInfos ControlText is the fallback lookup key.");
			}
			List<string> listFailures=new List<string>();
			foreach(XElement xElement in listPrefInfs) {
				foreach(string elementName in new[] { "ControlText","Details" }) {
					string text=(string)xElement.Element(elementName)??"";
					if(TermSources.RegexUcrWord.IsMatch(text)) {
						listFailures.Add((string)xElement.Element("ControlName")+" "+elementName);
					}
				}
			}
			Assert.AreEqual(0,listFailures.Count,"PrefInfos.xml text that still says UCR: "+string.Join(", ",listFailures));
		}
	}
}
