// Stage 0 of the .NET 10 move (S0-2a, docs/specs/net10-plan.md): Topaz signature pads are removed. TopazWrapper, which loaded Topaz's
// SigPlusNET.dll at run time (BIN-B), is gone; SignatureBoxWrapper keeps its public members, and a stored Topaz signature takes the path that
// "Topaz's software is not installed" took before: the label says Topaz signature pads are not supported, and the signature is not valid.
// These tests replace TopazWrapperTests, whose installed cases compiled a stand-in SigPlusNET.dll with CodeDom.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental.UI;

namespace BINBTests {
	[TestClass]
	public class TopazRemovedTests {
		///<summary>A string shaped like a Topaz signature (hex digits). It is valid base 64, so the program's own box tries to decrypt it, and fails.</summary>
		private const string SIG_TOPAZ_SHAPED="0A1B2C3D4E5F60718293A4B5C6D7E8F9";
		private const string KEY_DATA="synthetic treatment plan key data";

		private static Label GetLabel(SignatureBoxWrapper signatureBoxWrapper) {
			return (Label)signatureBoxWrapper.Controls.Find("labelInvalidSig",true).Single();
		}

		///<summary>The Images module's source, which must exist, and the old 2020 module's only while it exists: S0-6 (docs/specs/net10-plan.md)
		///deletes ControlImagesOld.cs after S0-2a merges, and from then on only ControlImages.cs is checked.</summary>
		private static List<string> GetImagesModuleSources() {
			List<string> listRelative=new List<string> { @"OpenPmsDental\Main Modules\ControlImages.cs" };
			string relativeOld=@"OpenPmsDental\Main Modules\ControlImagesOld.cs";
			if(File.Exists(Path.Combine(TestPaths.RepoRoot,relativeOld))) {
				listRelative.Add(relativeOld);
			}
			return listRelative;
		}

		///<summary>A signature made in the program's own box for a treatment plan (digitally signed, synthetic signer), as FormTPsign saves one.</summary>
		private static string GetOwnTreatPlanSignature(string keyData) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString(OpenPmsDentalBusiness.TreatPlans.GetHashStringForSignature(keyData));
				signatureBox.SetDigitalSig("Digitally Signed by Synthetic Test User");
				return signatureBox.GetSigString();
			}
		}

		///<summary>FormTPsign's fill of one treatment plan box: the SigIsTopaz column first, then the other kind when the first is not valid.</summary>
		private static void FillAsFormTPsign(SignatureBoxWrapper signatureBoxWrapper,bool sigIsTopaz,string keyData,string signature) {
			signatureBoxWrapper.FillSignature(sigIsTopaz,keyData,signature);
			if(!signatureBoxWrapper.IsValid) {
				signatureBoxWrapper.FillSignature(!sigIsTopaz,keyData,signature);
			}
		}

		[TestMethod]
		public void Message_SaysTopazIsNotSupported() {
			Assert.AreEqual("Topaz signature pads are not supported in this version of Open PMS - Dental.",SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE);
		}

		[TestMethod]
		public void TopazWrapper_IsGone() {
			Assert.IsFalse(File.Exists(Path.Combine(TestPaths.RepoRoot,"OpenPmsDentalBusiness","UI","TopazWrapper.cs")));
			Assert.IsNull(typeof(SignatureBoxWrapper).Assembly.GetType("OpenPmsDental.UI.TopazWrapper"),"TopazWrapper is no longer compiled.");
			Assert.IsFalse(TestPaths.ReadSource("OpenPmsDentalBusiness","OpenPmsDentalBusiness.csproj").Contains("TopazWrapper"));
			foreach(string relative in GetImagesModuleSources()
				.Concat(new[] { @"OpenPmsDentalBusiness\UI\SignatureBoxWrapper.cs",@"OpenPmsDentalBusiness\UI\SignatureBoxWrapper.Designer.cs" }))
			{
				string source=TestPaths.ReadSource(relative);
				Assert.IsFalse(source.Contains("TopazWrapper") || source.Contains("_sigBoxTopaz") || source.Contains("butTopazSign"),relative);
			}
		}

		[TestMethod]
		public void SignatureBoxWrapper_HasNoTopazControlOrButton() {
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				Assert.AreEqual(0,signatureBoxWrapper.Controls.Find("butTopazSign",true).Length,"The Sign Topaz button is gone.");
				Assert.AreEqual(0,signatureBoxWrapper.Controls.Find("sigBoxTopaz",true).Length,"No Topaz control is created.");
				Assert.IsFalse(signatureBoxWrapper.CheckTopaz());
				Assert.IsFalse(signatureBoxWrapper.GetSigIsTopaz());
				//The public members stay for their callers and do nothing Topaz-related.
				signatureBoxWrapper.SignTopazClicked+=(sender,e) => Assert.Fail("Never raised.");
				signatureBoxWrapper.SetControlSigBoxTopaz(new Control());
				signatureBoxWrapper.SetTabletState(1);
				signatureBoxWrapper.ClearSignature(clearTopazTablet:true);
				Assert.IsFalse(signatureBoxWrapper.GetSigIsTopaz());
				Assert.AreEqual(0,signatureBoxWrapper.GetNumberOfTabletPoints(true));
			}
		}

		[TestMethod]
		public void OwnSignature_IsSavedAsNotTopaz() {
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				string signature=GetOwnTreatPlanSignature(KEY_DATA);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,signature);
				Assert.IsTrue(signatureBoxWrapper.IsValid);
				Assert.IsFalse(signatureBoxWrapper.GetSigIsTopaz(),"Every signature is made in the program's own box.");
				Assert.AreNotEqual(0,signatureBoxWrapper.GetNumberOfTabletPoints(false));
			}
		}

		[TestMethod]
		public void StoredTopazSignature_ShowsTheMessage_ThenItsOwnTextAgain() {
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,"synthetic key data","FFFFFFFF00");
				Assert.AreEqual(SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE,labelInvalidSig.Text,"A Topaz signature can be neither shown nor checked.");
				Assert.IsTrue(labelInvalidSig.Visible);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,signatureBoxWrapper.LabelText,"Callers still see their own label text.");
				Assert.AreEqual(0,signatureBoxWrapper.GetNumberOfTabletPoints(true));
				signatureBoxWrapper.ClearSignature();
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				signatureBoxWrapper.LabelText="Changed by a caller";
				signatureBoxWrapper.FillSignature(true,"synthetic key data","FFFFFFFF00");
				signatureBoxWrapper.LabelText="Changed again while the message shows";
				signatureBoxWrapper.ClearSignature();
				Assert.AreEqual("Changed again while the message shows",labelInvalidSig.Text);
			}
		}

		[TestMethod]
		public void TreatPlanTriesTheOtherKind_TheMessageStays() {
			//FormTPsign and SheetDrawingJob.GetSigTPHelper: a Topaz signature that does not show is filled once more as the program's own.
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE,labelInvalidSig.Text,"Not 'Invalid Signature': Topaz could not check it.");
				Assert.IsTrue(labelInvalidSig.Visible);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(0,signatureBoxWrapper.GetNumberOfTabletPoints(false));
				Assert.AreEqual(textOwn,signatureBoxWrapper.LabelText,"Callers still see their own label text.");
				signatureBoxWrapper.ClearSignature();
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				Assert.IsFalse(labelInvalidSig.Visible);
			}
		}

		[TestMethod]
		public void OwnKindFirstThenTopaz_TheMessageShows() {
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE,labelInvalidSig.Text);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
			}
		}

		[TestMethod]
		public void TheOwnSignatureOfATopazMarkedPlanStillShows() {
			//One SigIsTopaz column for two boxes: the column says Topaz, but this box was signed in the program's own box.
			string signature=GetOwnTreatPlanSignature(KEY_DATA);
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped(signature),"The program's own box stores base 64, not Topaz's hex: "+signature);
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,signature);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,labelInvalidSig.Text,"Not a Topaz string, so no Topaz message.");
				signatureBoxWrapper.FillSignature(false,KEY_DATA,signature);
				Assert.IsTrue(signatureBoxWrapper.IsValid,"The program's own signature is shown and checked.");
				Assert.IsFalse(labelInvalidSig.Visible);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				//Changed key data (the plan changed after signing): the program's own check fails, and nothing Topaz could check is pending.
				signatureBoxWrapper.FillSignature(false,KEY_DATA+" changed",signature);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,labelInvalidSig.Text,"A failed check of the program's own signature still says so.");
			}
		}

		[TestMethod]
		public void OwnSignatureWithChangedKeyData_BothFormTPsignOrders_SayInvalidSignature() {
			string signature=GetOwnTreatPlanSignature(KEY_DATA);
			foreach(bool sigIsTopaz in new[] { true,false }) {
				using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
					signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
					Assert.AreEqual("Invalid Signature",GetLabel(signatureBoxWrapper).Text);
					FillAsFormTPsign(signatureBoxWrapper,sigIsTopaz,KEY_DATA+" changed",signature);
					Assert.IsFalse(signatureBoxWrapper.IsValid);
					Assert.IsTrue(GetLabel(signatureBoxWrapper).Visible);
					Assert.AreEqual("Invalid Signature",GetLabel(signatureBoxWrapper).Text,"A tampered or changed plan is still flagged. SigIsTopaz="+sigIsTopaz);
				}
			}
		}

		[TestMethod]
		public void TopazShapedSignature_BothFormTPsignOrders_TheMessageShows() {
			foreach(bool sigIsTopaz in new[] { true,false }) {
				using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
					signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
					FillAsFormTPsign(signatureBoxWrapper,sigIsTopaz,KEY_DATA,SIG_TOPAZ_SHAPED);
					Assert.IsFalse(signatureBoxWrapper.IsValid);
					Assert.AreEqual(SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE,GetLabel(signatureBoxWrapper).Text,"SigIsTopaz="+sigIsTopaz);
				}
			}
		}

		[TestMethod]
		public void AnotherSignatureAfterTheMessage_GetsTheOwnText() {
			//The message is kept only for the signature Topaz could not check, never for a different one.
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,"1A2B3C4D5E6F708192A3B4C5D6E7F809");
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				//After ClearSignature nothing is pending either.
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				signatureBoxWrapper.ClearSignature();
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
			}
		}

		[TestMethod]
		public void IsTopazShaped_HexOnly() {
			Assert.IsTrue(SignatureBoxWrapper.IsTopazShaped(SIG_TOPAZ_SHAPED));
			Assert.IsTrue(SignatureBoxWrapper.IsTopazShaped("ffff 0000\r\n"));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped(""));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped(null));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped("  "));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped("QUJDRA=="),"Base 64 padding is not hex.");
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped("0A1B+2C3/"));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped("45,68;48,70;0,0"),"Plain points are not hex.");
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped(GetOwnTreatPlanSignature(KEY_DATA)));
			Assert.IsFalse(SignatureBoxWrapper.IsTopazShaped(GetOwnTreatPlanSignature("another synthetic plan")));
		}

		[TestMethod]
		public void ImagesModules_ShowTheMessageForADocumentWithATopazSignature() {
			//The Images module and the old 2020 module (until S0-6 deletes it; GetImagesModuleSources) show the same text in place of a stored
			//Topaz signature.
			foreach(string relative in GetImagesModuleSources()) {
				string source=TestPaths.ReadSource(relative);
				int indexFill=source.IndexOf("private void FillSignature() {");
				int indexTopaz=source.IndexOf("SigIsTopaz) {",indexFill);
				int indexMessage=source.IndexOf("labelInvalidSig.Text=Lan.g(this,SignatureBoxWrapper.TOPAZ_NOT_SUPPORTED_MESSAGE);",indexTopaz);
				int indexOwn=source.IndexOf("else {//not topaz",indexTopaz);
				Assert.IsTrue(indexFill>0 && indexTopaz>indexFill && indexMessage>indexTopaz && indexMessage<indexOwn,relative);
			}
		}

		[TestMethod]
		public void SigPlusNet_StaysDeniedWithTheNewReason() {
			string buildRelease=TestPaths.ReadSource("tools","Build-Release.ps1");
			StringAssert.Contains(buildRelease,"@('SigPlusNET.dll','Topaz: no published redistribution terms; Topaz signature pads were removed in stage 0");
		}
	}
}
