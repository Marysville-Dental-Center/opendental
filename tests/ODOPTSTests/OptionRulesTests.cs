// ODOPTS without a database: the eight names, the defaults of missing rows, and the rules of each option on made-up values.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations.FeatureSteps;

namespace ODOPTSTests {
	[TestClass]
	public class OptionRulesTests {
		[TestInitialize]
		public void TestInitialize() {
			ScratchDb.SetCulture();
		}

		#region The names
		[TestMethod]
		public void TheEightNames_AreOpenDentalsNames_ReadByName() {
			CollectionAssert.AreEqual(new[] { "PerioRedAttGing","DomainLoginForSigBoxes","ClaimUpdateFeeForSecondary","EFormImportAllergiesAsOther",
				"EFormImportProblemsAsOther","InsHistPerioScalingCode","StatementAutoSendEmail","StatementAutoSendSMS" },OdOptions.ArrayPrefNames);
			foreach(string name in OdOptions.ArrayPrefNames) {
				Assert.IsFalse(Enum.IsDefined(typeof(PrefName),name),name+" is read by name, so a database without the row reads as the default.");
			}
		}

		[TestMethod]
		public void MigrationPreview_ListsTheEight_WithWhatThisProgramShows() {
			List<string> listNamesRead=OdSettingNames.ListReads.SelectMany(x => x.ArrayPrefNamesOd).ToList();
			foreach(string name in OdOptions.ArrayPrefNames) {
				Assert.AreEqual(1,listNamesRead.Count(x => x==name),name);
			}
			//No row: the defaults.
			List<string> listLines=OdSettingNames.DescribeReads(x => null);
			CollectionAssert.Contains(listLines,"Perio Chart: auto Att Ging red if at or below (mm) (ODOPTS): none (never red) [PerioRedAttGing no row]");
			CollectionAssert.Contains(listLines,"Offer domain login in the Change User of signature boxes (ODOPTS): ticked [DomainLoginForSigBoxes no row]");
			CollectionAssert.Contains(listLines,ClaimFeeForSecondary.LABEL+" (ODOPTS): not ticked [ClaimUpdateFeeForSecondary no row]");
			CollectionAssert.Contains(listLines,"eForm import: an allergy matching no list entry is imported as Other (ODOPTS): ticked [EFormImportAllergiesAsOther no row]");
			CollectionAssert.Contains(listLines,"eForm import: a problem matching no list entry is imported as Other (ODOPTS): ticked [EFormImportProblemsAsOther no row]");
			CollectionAssert.Contains(listLines,"Insurance History perio scaling procedure code (ODOPTS): blank (each perio scaling code group's History Proc Code) "
				+"[InsHistPerioScalingCode no row]");
			CollectionAssert.Contains(listLines,"Send statements by e-mail when billing creates them (ODOPTS): Never [StatementAutoSendEmail no row]");
			CollectionAssert.Contains(listLines,"Send statements by text when billing creates them (ODOPTS): Never (no texting provider: nothing is texted) "
				+"[StatementAutoSendSMS no row]");
			//Made-up stored values, read with this program's meaning.
			Dictionary<string,string> dictRows=new Dictionary<string,string> {
				{ "PerioRedAttGing","1" },{ "DomainLoginForSigBoxes","0" },{ "ClaimUpdateFeeForSecondary","1" },{ "EFormImportAllergiesAsOther","1" },
				{ "EFormImportProblemsAsOther","0" },{ "InsHistPerioScalingCode","Z4341" },{ "StatementAutoSendEmail","1" },{ "StatementAutoSendSMS","9" },
			};
			listLines=OdSettingNames.DescribeReads(x => dictRows.TryGetValue(x,out string value) ? value : null);
			CollectionAssert.Contains(listLines,"Perio Chart: auto Att Ging red if at or below (mm) (ODOPTS): 1 [PerioRedAttGing='1']");
			CollectionAssert.Contains(listLines,"Offer domain login in the Change User of signature boxes (ODOPTS): not ticked [DomainLoginForSigBoxes='0']");
			CollectionAssert.Contains(listLines,"eForm import: a problem matching no list entry is imported as Other (ODOPTS): not ticked [EFormImportProblemsAsOther='0']");
			CollectionAssert.Contains(listLines,"Insurance History perio scaling procedure code (ODOPTS): Z4341 [InsHistPerioScalingCode='Z4341']");
			CollectionAssert.Contains(listLines,"Send statements by e-mail when billing creates them (ODOPTS): Ask [StatementAutoSendEmail='1']");
			CollectionAssert.Contains(listLines,"Send statements by text when billing creates them (ODOPTS): Never (no texting provider: nothing is texted) "
				+"[StatementAutoSendSMS='9']","An unknown value never sends.");
		}

		[TestMethod]
		public void BoolRows_BlankOrMissingReadsAsTheDefault() {
			Assert.IsTrue(OdOptions.GetBoolFromValue(null,true));
			Assert.IsFalse(OdOptions.GetBoolFromValue(null,false));
			Assert.IsTrue(OdOptions.GetBoolFromValue("  ",true));
			Assert.IsFalse(OdOptions.GetBoolFromValue("0",true));
			Assert.IsTrue(OdOptions.GetBoolFromValue("1",false));
			Assert.IsTrue(OdOptions.GetBoolFromValue(" 1 ",false));
		}

		[TestMethod]
		public void LogText_NamesTheSettingTheRowAndBothValues() {
			Assert.AreEqual("Some setting (SomeRow) changed from 'blank' to 'Z4341'.",OdOptions.GetChangeLogText("Some setting","SomeRow","","Z4341"));
			Assert.AreEqual("Some setting (SomeRow) changed from 'on' to 'off'.",OdOptions.GetChangeLogText("Some setting","SomeRow","on","off"));
		}
		#endregion The names

		#region PerioRedAttGing
		[TestMethod]
		public void PerioRedAttGing_StoredValues() {
			Assert.IsNull(PerioMeasures.GetRedAttGingFromValue(null),"No row: no threshold.");
			Assert.IsNull(PerioMeasures.GetRedAttGingFromValue(""),"Blank: no threshold.");
			Assert.IsNull(PerioMeasures.GetRedAttGingFromValue("x"));
			Assert.AreEqual(2,PerioMeasures.GetRedAttGingFromValue(" 2 "));
			Assert.AreEqual(0,PerioMeasures.GetRedAttGingFromValue("0"));
			Assert.AreEqual("",PerioMeasures.GetRedAttGingValue(null));
			Assert.AreEqual("1",PerioMeasures.GetRedAttGingValue(1));
		}

		[TestMethod]
		public void PerioRedAttGing_AtOrBelowIsRed() {
			Assert.IsFalse(PerioMeasures.IsRedAttGing(0,null),"Without a threshold nothing is red.");
			Assert.IsFalse(PerioMeasures.IsRedAttGing(-3,null));
			Assert.IsTrue(PerioMeasures.IsRedAttGing(1,1),"At the threshold: red.");
			Assert.IsTrue(PerioMeasures.IsRedAttGing(0,1));
			Assert.IsTrue(PerioMeasures.IsRedAttGing(-2,1),"No attached gingiva (below zero): red.");
			Assert.IsFalse(PerioMeasures.IsRedAttGing(2,1),"Above: not red.");
			Assert.IsFalse(PerioMeasures.IsRedAttGing(null,1),"A blank site is never red.");
		}

		[TestMethod]
		public void PerioRedAttGing_UpAndDownButtons() {
			Assert.AreEqual(0,PerioMeasures.GetRedAttGingNext(null,isUp:true),"Up from none starts at 0.");
			Assert.AreEqual(3,PerioMeasures.GetRedAttGingNext(2,isUp:true));
			Assert.AreEqual(PerioMeasures.RED_ATT_GING_MAX,PerioMeasures.GetRedAttGingNext(PerioMeasures.RED_ATT_GING_MAX,isUp:true));
			Assert.AreEqual(1,PerioMeasures.GetRedAttGingNext(2,isUp:false));
			Assert.IsNull(PerioMeasures.GetRedAttGingNext(0,isUp:false),"Down from 0 removes the threshold.");
			Assert.IsNull(PerioMeasures.GetRedAttGingNext(null,isUp:false));
			Assert.IsNull(PerioMeasures.GetRedAttGingNext(-4,isUp:false),"A negative stored value goes to none.");
		}
		#endregion PerioRedAttGing

		#region DomainLoginForSigBoxes
		[TestMethod]
		public void DomainLoginForSigBoxes_OnlySignatureBoxesFollowTheSwitch() {
			Assert.IsFalse(DomainLogin.IsOfferedInChangeUser(isSigBox:true,isDomainLoginUsable:false,isForSigBoxesOn:true),"Domain login off: never.");
			Assert.IsFalse(DomainLogin.IsOfferedInChangeUser(isSigBox:false,isDomainLoginUsable:false,isForSigBoxesOn:true));
			Assert.IsTrue(DomainLogin.IsOfferedInChangeUser(isSigBox:true,isDomainLoginUsable:true,isForSigBoxesOn:true));
			Assert.IsFalse(DomainLogin.IsOfferedInChangeUser(isSigBox:true,isDomainLoginUsable:true,isForSigBoxesOn:false),"Switch off: not in sig boxes.");
			Assert.IsTrue(DomainLogin.IsOfferedInChangeUser(isSigBox:false,isDomainLoginUsable:true,isForSigBoxesOn:false),
				"A task's From User is not a signature box.");
		}
		#endregion DomainLoginForSigBoxes

		#region ClaimUpdateFeeForSecondary
		private static Claim NewClaim(long claimNum,string claimType,string claimStatus) {
			return new Claim { ClaimNum=claimNum,ClaimType=claimType,ClaimStatus=claimStatus };
		}

		private static ClaimProc NewClaimProc(long procNum,long claimNum,ClaimProcStatus status,bool isOverpay=false) {
			return new ClaimProc { ProcNum=procNum,ClaimNum=claimNum,Status=status,IsOverpay=isOverpay };
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_OnlyUnsentSecondaryClaimsOfAProcOnAPrimaryClaim() {
			List<Claim> listClaims=new List<Claim> { NewClaim(11,"P","S"),NewClaim(12,"S","H"),NewClaim(13,"S","S"),NewClaim(14,"S","U"),
				NewClaim(15,"PreAuth","U") };
			List<ClaimProc> listClaimProcs=new List<ClaimProc> {
				NewClaimProc(1,11,ClaimProcStatus.NotReceived),
				NewClaimProc(1,12,ClaimProcStatus.NotReceived),
				NewClaimProc(1,13,ClaimProcStatus.NotReceived),//sent: keeps its fee
				NewClaimProc(1,14,ClaimProcStatus.NotReceived),
				NewClaimProc(1,14,ClaimProcStatus.NotReceived,isOverpay:true),//the same claim once
				NewClaimProc(1,15,ClaimProcStatus.Preauth),
				NewClaimProc(1,0,ClaimProcStatus.Estimate),
				NewClaimProc(2,12,ClaimProcStatus.NotReceived),//another procedure
			};
			CollectionAssert.AreEqual(new long[] { 12,14 },ClaimFeeForSecondary.GetSecondaryClaimsToUpdate(1,listClaimProcs,listClaims)
				.Select(x => x.ClaimNum).ToArray());
			//Received primary: the secondary claim still follows the fee.
			listClaims[0].ClaimStatus="R";
			listClaimProcs[0].Status=ClaimProcStatus.Received;
			Assert.AreEqual(2,ClaimFeeForSecondary.GetSecondaryClaimsToUpdate(1,listClaimProcs,listClaims).Count);
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_NothingWithoutAPrimaryClaim() {
			List<Claim> listClaims=new List<Claim> { NewClaim(12,"S","H") };
			List<ClaimProc> listClaimProcs=new List<ClaimProc> { NewClaimProc(1,12,ClaimProcStatus.NotReceived) };
			Assert.AreEqual(0,ClaimFeeForSecondary.GetSecondaryClaimsToUpdate(1,listClaimProcs,listClaims).Count);
			Assert.AreEqual(0,ClaimFeeForSecondary.GetSecondaryClaimsToUpdate(1,null,listClaims).Count);
			//A received claimproc on the secondary claim is not updated.
			listClaims.Add(NewClaim(11,"P","S"));
			listClaimProcs.Add(NewClaimProc(1,11,ClaimProcStatus.NotReceived));
			listClaimProcs[0].Status=ClaimProcStatus.Received;
			Assert.AreEqual(0,ClaimFeeForSecondary.GetSecondaryClaimsToUpdate(1,listClaimProcs,listClaims).Count);
		}

		[TestMethod]
		public void ClaimUpdateFeeForSecondary_IsFeeOpen() {
			foreach(string status in new[] { "U","H","W","I" }) {
				Assert.IsTrue(ClaimFeeForSecondary.IsFeeOpen(NewClaim(1,"S",status)),status);
			}
			Assert.IsFalse(ClaimFeeForSecondary.IsFeeOpen(NewClaim(1,"S","S")));
			Assert.IsFalse(ClaimFeeForSecondary.IsFeeOpen(NewClaim(1,"S","R")));
			Assert.IsFalse(ClaimFeeForSecondary.IsFeeOpen(null));
		}
		#endregion ClaimUpdateFeeForSecondary

		#region InsHistPerioScalingCode
		private static List<CodeGroup> CodeGroupsForHistory() {
			return new List<CodeGroup> {
				new CodeGroup { CodeGroupNum=1,GroupName="BWs",ProcCodes="Z0272",ItemOrder=0,CodeGroupFixed=EnumCodeGroupFixed.BW,ShowInHistory=true,
					HistProcCode="Z0272" },
				new CodeGroup { CodeGroupNum=2,GroupName="SRP",ProcCodes="Z4341",ItemOrder=1,CodeGroupFixed=EnumCodeGroupFixed.SRP,ShowInHistory=true,
					HistProcCode="Z4341",IsPerioFourQuads=true },
				new CodeGroup { CodeGroupNum=3,GroupName="Perio Maint",ProcCodes="Z4910",ItemOrder=2,CodeGroupFixed=EnumCodeGroupFixed.Perio,
					ShowInHistory=true,HistProcCode="Z4910" },
			};
		}

		[TestMethod]
		public void InsHistPerioScalingCode_UsedByEveryPerioScalingRow() {
			List<InsHistCategory> listCategories=InsHistCategories.GetAll(CodeGroupsForHistory(),"Z4399");
			Assert.AreEqual(6,listCategories.Count);
			foreach(InsHistCategory category in listCategories.FindAll(x => x.CodeGroupNum==2)) {
				Assert.AreEqual("Z4399",category.HistProcCode,category.Description);
				Assert.IsTrue(category.IsMatch("Z4399",category.Quadrant),"The setting's procedures count as the row's history.");
				Assert.IsTrue(category.IsMatch("Z4341",category.Quadrant),"The group's own codes still count.");
			}
			Assert.AreEqual(4,listCategories.Count(x => x.CodeGroupNum==2));
			Assert.AreEqual("Z0272",listCategories.Single(x => x.CodeGroupNum==1).HistProcCode,"Other rows keep their own code.");
			Assert.AreEqual("Z4910",listCategories.Single(x => x.CodeGroupNum==3).HistProcCode);
		}

		[TestMethod]
		public void InsHistPerioScalingCode_BlankKeepsEachGroupsOwnCode() {
			foreach(string blank in new[] { "",null,"  " }) {
				List<InsHistCategory> listCategories=InsHistCategories.GetAll(CodeGroupsForHistory(),blank);
				Assert.IsTrue(listCategories.FindAll(x => x.CodeGroupNum==2).All(x => x.HistProcCode=="Z4341"));
			}
			//Without a preference cache (no database) the setting reads as blank: the behaviour without it.
			Assert.IsTrue(InsHistCategories.GetAll(CodeGroupsForHistory()).FindAll(x => x.CodeGroupNum==2).All(x => x.HistProcCode=="Z4341"));
		}

		[TestMethod]
		public void InsHistPerioScalingCode_ReadsACodeOrACodeNum() {
			Dictionary<string,string> dictCodes=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { { "Z4342","Z4342" },{ "11111","11111" } };
			Dictionary<long,string> dictCodeNums=new Dictionary<long,string> { { 77,"Z4342" },{ 11111,"Z9999" } };
			Func<string,string> funcGetProcCode=x => dictCodes.TryGetValue(x,out string code) ? code : "";
			Func<long,string> funcByCodeNum=x => dictCodeNums.TryGetValue(x,out string code) ? code : "";
			Assert.AreEqual("Z4342",InsHistCategories.GetPerioScalingProcCode(" z4342 ",funcGetProcCode,funcByCodeNum),"As the list spells it.");
			Assert.AreEqual("Z4342",InsHistCategories.GetPerioScalingProcCode("77",funcGetProcCode,funcByCodeNum),"A CodeNum.");
			Assert.AreEqual("11111",InsHistCategories.GetPerioScalingProcCode("11111",funcGetProcCode,funcByCodeNum),"A numeric code wins over a CodeNum.");
			Assert.AreEqual("",InsHistCategories.GetPerioScalingProcCode("Z0000",funcGetProcCode,funcByCodeNum),"No such code.");
			Assert.AreEqual("",InsHistCategories.GetPerioScalingProcCode("",funcGetProcCode,funcByCodeNum));
		}
		#endregion InsHistPerioScalingCode

		#region StatementAutoSendEmail and StatementAutoSendSMS
		private class FakeTextSender:IStatementTextSender {
			public List<long> ListStatementNumsSent=new List<long>();

			public int SendTexts(List<long> listStatementNums) {
				ListStatementNumsSent.AddRange(listStatementNums);
				return listStatementNums.Count;
			}
		}

		[TestMethod]
		public void StatementAutoSend_Values() {
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue(null),"No row: never.");
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue(""));
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue("0"));
			Assert.AreEqual(StatementAutoSendMode.Ask,StatementAutoSend.GetModeFromValue("1"));
			Assert.AreEqual(StatementAutoSendMode.Always,StatementAutoSend.GetModeFromValue(" 2 "));
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue("3"),"An unknown value never sends.");
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue("-1"));
			Assert.AreEqual(StatementAutoSendMode.Never,StatementAutoSend.GetModeFromValue("yes"));
			Assert.AreEqual("2",StatementAutoSend.GetValue(StatementAutoSendMode.Always));
			Assert.AreEqual("Ask",StatementAutoSend.GetDescription(StatementAutoSendMode.Ask));
		}

		[TestMethod]
		public void StatementAutoSend_CreatedStatements() {
			CollectionAssert.AreEqual(new long[] { 7,9 },StatementAutoSend.GetCreatedStatementNums(new List<long> { 3,5 },new List<long> { 3,7,5,9,9 }));
			CollectionAssert.AreEqual(new long[] { 4 },StatementAutoSend.GetCreatedStatementNums(null,new List<long> { 4 }));
			Assert.AreEqual(0,StatementAutoSend.GetCreatedStatementNums(new List<long> { 4 },null).Count);
		}

		[TestMethod]
		public void StatementAutoSendEmail_NeverAskAlways() {
			List<long> listCreated=new List<long> { 21,22 };
			int countAsked=0;
			Func<int,bool> funcYes=x => { countAsked++; Assert.AreEqual(2,x,"Asked once, with the count."); return true; };
			Func<int,bool> funcNo=x => { countAsked++; return false; };
			Assert.AreEqual(0,StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSendMode.Never,listCreated,funcYes).Count);
			Assert.AreEqual(0,countAsked,"Never does not ask.");
			CollectionAssert.AreEqual(listCreated,StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSendMode.Always,listCreated,funcNo));
			Assert.AreEqual(0,countAsked,"Always does not ask.");
			CollectionAssert.AreEqual(listCreated,StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSendMode.Ask,listCreated,funcYes));
			Assert.AreEqual(0,StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSendMode.Ask,listCreated,funcNo).Count);
			Assert.AreEqual(2,countAsked);
			Assert.AreEqual(0,StatementAutoSend.GetEmailStatementNumsToSend(StatementAutoSendMode.Ask,new List<long>(),funcYes).Count);
			Assert.AreEqual(2,countAsked,"Nothing created: not asked.");
		}

		[TestMethod]
		public void StatementAutoSendSMS_NoProvider_NothingIsTexted() {
			List<long> listCreated=new List<long> { 31,32,33 };
			StatementTextResult result=StatementAutoSend.SendTexts(StatementAutoSendMode.Always,listCreated,x => true,textSender:null);
			Assert.AreEqual(0,result.CountSent);
			StringAssert.Contains(result.Message,"not texted");
			StringAssert.Contains(result.Message,StatementAutoSend.TEXT_NEEDS_PROVIDER);
			Assert.AreEqual("",StatementAutoSend.SendTexts(StatementAutoSendMode.Never,listCreated,x => true,null).Message,"Never: nothing to say.");
			Assert.AreEqual("",StatementAutoSend.SendTexts(StatementAutoSendMode.Always,new List<long>(),x => true,null).Message,"Nothing created.");
			Assert.IsNull(StatementAutoSend.TextSender,"This program has no texting provider.");
		}

		[TestMethod]
		public void StatementAutoSendSMS_TheHook_WithAProvider() {
			List<long> listCreated=new List<long> { 31,32 };
			FakeTextSender fakeTextSender=new FakeTextSender();
			Assert.AreEqual(2,StatementAutoSend.SendTexts(StatementAutoSendMode.Always,listCreated,x => false,fakeTextSender).CountSent);
			Assert.AreEqual(0,StatementAutoSend.SendTexts(StatementAutoSendMode.Ask,listCreated,x => false,fakeTextSender).CountSent);
			Assert.AreEqual(2,StatementAutoSend.SendTexts(StatementAutoSendMode.Ask,listCreated,x => true,fakeTextSender).CountSent);
			Assert.AreEqual(0,StatementAutoSend.SendTexts(StatementAutoSendMode.Never,listCreated,x => true,fakeTextSender).CountSent);
			CollectionAssert.AreEqual(new long[] { 31,32,31,32 },fakeTextSender.ListStatementNumsSent);
		}
		#endregion StatementAutoSendEmail and StatementAutoSendSMS
	}
}
