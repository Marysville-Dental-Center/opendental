// The Topaz signature pad without SigPlusNET.dll in the build: absent, the program says so; present (a synthetic stand-in compiled at test time,
// never Topaz's file), the wrapper loads it from the search folders at run time and makes the same calls the compiled code made.
// Licensed under the GNU General Public License, version 2 or later. See LICENSE in the repository root.
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDental.UI;

namespace BINBTests {
	[TestClass]
	public class TopazWrapperTests {
		///<summary>A stand-in with the same type name and the public methods the wrapper calls. Every call is recorded in the static Calls list.
		///SetSigString("valid") gives 250 points; "needs-bytes" gives points only after the private Sig.SetAutoKeyData(byte[]) was called; "throw"
		///throws as Topaz's control does for a corrupt signature.</summary>
		private const string FAKE_SOURCE=@"
using System;
using System.Collections.Generic;
using System.Windows.Forms;
namespace Topaz {
	public class Signature {
		public bool IsKeyBytesSet;
		private void SetAutoKeyData(byte[] arrayBytes) { SigPlusNET.Calls.Add(""Sig.SetAutoKeyData""); IsKeyBytesSet=true; }
	}
	public class SigPlusNET:Control {
		public static List<string> Calls=new List<string>();
		private Signature Sig=new Signature();
		private int _points;
		private int _state;
		private string _sig="""";
		public void ClearTablet() { Calls.Add(""ClearTablet""); _points=0; }
		public void SetTabletLogicalXSize(int x) { Calls.Add(""SetTabletLogicalXSize:""+x); }
		public void SetTabletLogicalYSize(int y) { Calls.Add(""SetTabletLogicalYSize:""+y); }
		public void SetSigCompressionMode(int mode) { Calls.Add(""SetSigCompressionMode:""+mode); }
		public void SetEncryptionMode(int mode) { Calls.Add(""SetEncryptionMode:""+mode); }
		public void SetKeyString(string key) { Calls.Add(""SetKeyString:""+key); }
		public void AutoKeyStart() { Calls.Add(""AutoKeyStart""); }
		public void AutoKeyFinish() { Calls.Add(""AutoKeyFinish""); }
		public void SetAutoKeyData(string data) { Calls.Add(""SetAutoKeyData:""+data); }
		public void SetAutoKeyANSIData(string data) { Calls.Add(""SetAutoKeyANSIData:""+data); }
		public void SetSigString(string sig) {
			Calls.Add(""SetSigString:""+sig);
			if(sig==""throw"") { throw new IndexOutOfRangeException(""corrupt signature""); }
			_sig=sig;
			_points=(sig==""valid"" || (sig==""needs-bytes"" && Sig.IsKeyBytesSet)) ? 250 : 0;
		}
		public string GetSigString() { Calls.Add(""GetSigString""); return _sig; }
		public int NumberOfTabletPoints() { return _points; }
		public void SetTabletState(int state) { Calls.Add(""SetTabletState:""+state); _state=state; }
		public int GetTabletState() { return _state; }
	}
}";
		private static string _folderFake;
		private static string _folderEmpty;

		[ClassInitialize]
		public static void ClassInitialize(TestContext testContext) {
			_folderEmpty=TestPaths.NewTempFolder("topaz_none");
			_folderFake=TestPaths.NewTempFolder("topaz_fake");
			using(CSharpCodeProvider cSharpCodeProvider=new CSharpCodeProvider()) {
				CompilerParameters compilerParameters=new CompilerParameters(new[] { "System.dll","System.Windows.Forms.dll" },
					Path.Combine(_folderFake,TopazWrapper.ASSEMBLY_FILE_NAME));
				compilerParameters.GenerateExecutable=false;
				CompilerResults compilerResults=cSharpCodeProvider.CompileAssemblyFromSource(compilerParameters,FAKE_SOURCE);
				Assert.AreEqual(0,compilerResults.Errors.Count,string.Join("; ",compilerResults.Errors.Cast<CompilerError>().Select(x => x.ErrorText)));
			}
		}

		[ClassCleanup]
		public static void ClassCleanup() {
			TopazWrapper.ResetLoader();
		}

		private static List<string> GetCalls(Control control) {
			return (List<string>)control.GetType().GetField("Calls").GetValue(null);
		}

		[TestMethod]
		public void Message_IsTheOneTheOwnerAskedFor() {
			Assert.AreEqual("Topaz signature pad software is not installed.",TopazWrapper.NOT_INSTALLED_MESSAGE);
		}

		[TestMethod]
		public void NotInstalled_GetTopazThrowsTheNotInstalledMessage() {
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			Assert.AreEqual("",TopazWrapper.FindAssemblyPath());
			Assert.IsFalse(TopazWrapper.IsInstalled());
			TopazWrapper.TopazNotInstalledException topazNotInstalledException=Expect.Throws<TopazWrapper.TopazNotInstalledException>(
				() => TopazWrapper.GetTopaz());
			Assert.AreEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,topazNotInstalledException.Message);
		}

		[TestMethod]
		public void NotInstalled_EveryCallOnTheMissingControlDoesNothing() {
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			Assert.AreEqual(0,TopazWrapper.GetTopazNumberOfTabletPoints(null));
			Assert.AreEqual(0,TopazWrapper.GetTopazState(null));
			Assert.AreEqual("",TopazWrapper.GetTopazString(null));
			TopazWrapper.ClearTopaz(null);
			TopazWrapper.SetTopazCompressionMode(null,2);
			TopazWrapper.SetTopazEncryptionMode(null,2);
			TopazWrapper.SetTopazKeyString(null,"0000000000000000");
			TopazWrapper.SetTopazAutoKeyData(null,"key");
			TopazWrapper.SetTopazAutoKeyANSIData(null,"key");
			TopazWrapper.SetTopazSigString(null,"sig");
			TopazWrapper.SetTopazState(null,1);
			TopazWrapper.FillSignatureANSI(null,"key","sig",SignatureBoxWrapper.SigMode.Default);
			TopazWrapper.FillSignatureEncodings(null,"key","sig",SignatureBoxWrapper.SigMode.Default);
		}

		[TestMethod]
		public void NotInstalled_SignatureBoxShowsTheMessage_ThenItsOwnTextAgain() {
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				Label labelInvalidSig=(Label)signatureBoxWrapper.Controls.Find("labelInvalidSig",true).Single();
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,"synthetic key data","FFFFFFFF00");
				Assert.AreEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,labelInvalidSig.Text,"A Topaz signature cannot be shown or checked without Topaz.");
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

		///<summary>A string shaped like a Topaz signature (hex digits). It is valid base 64, so the program's own box tries to decrypt it, and fails.</summary>
		private const string SIG_TOPAZ_SHAPED="0A1B2C3D4E5F60718293A4B5C6D7E8F9";
		private const string KEY_DATA="synthetic treatment plan key data";

		private static Label GetLabel(SignatureBoxWrapper signatureBoxWrapper) {
			return (Label)signatureBoxWrapper.Controls.Find("labelInvalidSig",true).Single();
		}

		///<summary>A signature made in the program's own box for a treatment plan (digitally signed, synthetic signer), as FormTPsign saves one.</summary>
		private static string GetOwnTreatPlanSignature(string keyData) {
			using(SignatureBox signatureBox=new SignatureBox()) {
				signatureBox.SetKeyString(OpenDentBusiness.TreatPlans.GetHashStringForSignature(keyData));
				signatureBox.SetDigitalSig("Digitally Signed by Synthetic Test User");
				return signatureBox.GetSigString();
			}
		}

		[TestMethod]
		public void NotInstalled_TreatPlanTriesTheOtherKind_TheMessageStays() {
			//FormTPsign and SheetDrawingJob.GetSigTPHelper: a Topaz signature that does not show is filled once more as the program's own.
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,labelInvalidSig.Text,"Not 'Invalid Signature': Topaz could not check it.");
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
		public void NotInstalled_OwnKindFirstThenTopaz_TheMessageShows() {
			//The same retry when the SigIsTopaz column says the program's own box: both orders end with the same message.
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.AreEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,labelInvalidSig.Text);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
			}
		}

		[TestMethod]
		public void NotInstalled_TheOwnSignatureOfATopazMarkedPlanStillShows() {
			//One SigIsTopaz column for two boxes: the column says Topaz, but this box was signed in the program's own box.
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
			string signature=GetOwnTreatPlanSignature(KEY_DATA);
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,signature);
				Assert.AreEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,labelInvalidSig.Text);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,signature);
				Assert.IsTrue(signatureBoxWrapper.IsValid,"The program's own signature is shown and checked without Topaz.");
				Assert.IsFalse(labelInvalidSig.Visible);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				Assert.AreNotEqual(0,signatureBoxWrapper.GetNumberOfTabletPoints(false));
				//Changed key data (the plan changed after signing): the program's own check fails, and nothing Topaz could check is pending.
				signatureBoxWrapper.FillSignature(false,KEY_DATA+" changed",signature);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,labelInvalidSig.Text,"A failed check of the program's own signature still says so.");
			}
		}

		[TestMethod]
		public void NotInstalled_AnotherSignatureAfterTheMessage_GetsTheOwnText() {
			//The message is kept only for the signature Topaz could not check, never for a different one.
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty });
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
		public void Installed_TreatPlanTriesTheOtherKind_InvalidAsBefore() {
			//With Topaz installed a Topaz signature that fails both checks says the label's own text, as before.
			TopazWrapper.ResetLoader(new List<string> { _folderFake });
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				signatureBoxWrapper.SignatureMode=SignatureBoxWrapper.SigMode.TreatPlan;
				Label labelInvalidSig=GetLabel(signatureBoxWrapper);
				string textOwn=labelInvalidSig.Text;
				signatureBoxWrapper.FillSignature(true,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
				signatureBoxWrapper.FillSignature(false,KEY_DATA,SIG_TOPAZ_SHAPED);
				Assert.IsFalse(signatureBoxWrapper.IsValid);
				Assert.AreEqual(textOwn,labelInvalidSig.Text);
			}
		}

		[TestMethod]
		public void Installed_LoadsTheControlFromASearchFolderAtRunTime() {
			TopazWrapper.ResetLoader(new List<string> { _folderEmpty,_folderFake });
			string pathExpected=Path.Combine(_folderFake,TopazWrapper.ASSEMBLY_FILE_NAME);
			Assert.AreEqual(pathExpected,TopazWrapper.FindAssemblyPath());
			Assert.IsTrue(TopazWrapper.IsInstalled());
			using(Control control=TopazWrapper.GetTopaz()) {
				Assert.AreEqual(TopazWrapper.CONTROL_TYPE_NAME,control.GetType().FullName);
				Assert.AreEqual(Path.GetFullPath(pathExpected).ToLowerInvariant(),Path.GetFullPath(control.GetType().Assembly.Location).ToLowerInvariant());
			}
		}

		[TestMethod]
		public void Installed_WrapperMakesTheSameCallsAsTheCompiledCode() {
			TopazWrapper.ResetLoader(new List<string> { _folderFake });
			using(Control control=TopazWrapper.GetTopaz()) {
				List<string> listCalls=GetCalls(control);
				listCalls.Clear();
				TopazWrapper.ClearTopaz(control);
				TopazWrapper.SetTopazCompressionMode(control,2);
				TopazWrapper.SetTopazEncryptionMode(control,3);
				TopazWrapper.SetTopazKeyString(control,"0000000000000000");
				TopazWrapper.SetTopazAutoKeyData(control,"key one");
				TopazWrapper.SetTopazAutoKeyANSIData(control,"key two");
				TopazWrapper.SetTopazState(control,1);
				TopazWrapper.SetTopazSigString(control,"valid");
				CollectionAssert.AreEqual(new[] { "ClearTablet","SetTabletLogicalXSize:2000","SetTabletLogicalYSize:600","SetSigCompressionMode:2",
					"SetEncryptionMode:3","SetKeyString:0000000000000000","AutoKeyStart","SetAutoKeyData:key one","AutoKeyFinish","AutoKeyStart",
					"SetAutoKeyANSIData:key two","AutoKeyFinish","SetTabletState:1","SetSigString:valid" },listCalls);
				Assert.AreEqual(1,TopazWrapper.GetTopazState(control));
				Assert.AreEqual(250,TopazWrapper.GetTopazNumberOfTabletPoints(control));
				Assert.AreEqual("valid",TopazWrapper.GetTopazString(control));
				TopazWrapper.SetTopazSigString(control,"throw");//Topaz throws for a corrupt signature; the wrapper swallows it, as before.
			}
		}

		[TestMethod]
		public void Installed_FillSignatureEncodings_UsesTheControlsPrivateKeyMethod() {
			TopazWrapper.ResetLoader(new List<string> { _folderFake });
			using(Control control=TopazWrapper.GetTopaz()) {
				List<string> listCalls=GetCalls(control);
				listCalls.Clear();
				TopazWrapper.FillSignatureEncodings(control,"key","needs-bytes",SignatureBoxWrapper.SigMode.Default);
				Assert.AreEqual(250,TopazWrapper.GetTopazNumberOfTabletPoints(control));
				Assert.AreEqual(1,listCalls.Count(x => x=="Sig.SetAutoKeyData"),"Stops at the first encoding that shows the signature.");
			}
		}

		[TestMethod]
		public void Installed_SignatureBoxShowsATopazSignature() {
			TopazWrapper.ResetLoader(new List<string> { _folderFake });
			using(SignatureBoxWrapper signatureBoxWrapper=new SignatureBoxWrapper()) {
				Label labelInvalidSig=(Label)signatureBoxWrapper.Controls.Find("labelInvalidSig",true).Single();
				signatureBoxWrapper.FillSignature(true,"synthetic key data","valid");
				Assert.AreEqual(250,signatureBoxWrapper.GetNumberOfTabletPoints(true));
				Assert.AreEqual(signatureBoxWrapper.LabelText,labelInvalidSig.Text);
				Assert.AreNotEqual(TopazWrapper.NOT_INSTALLED_MESSAGE,labelInvalidSig.Text);
			}
		}

		[TestMethod]
		public void DefaultSearch_StartsWithTheProgramFolder() {
			TopazWrapper.ResetLoader();
			List<string> listFolders=TopazWrapper.GetSearchFolders();
			Assert.AreEqual(AppDomain.CurrentDomain.BaseDirectory,listFolders[0]);
			Assert.IsTrue(listFolders.Any(x => x.EndsWith("\\SigPlus",StringComparison.OrdinalIgnoreCase)),string.Join("; ",listFolders));
			Assert.IsTrue(listFolders.Skip(1).All(x => x.IndexOf("Topaz",StringComparison.OrdinalIgnoreCase)>=0 || x.EndsWith("\\SigPlus")),
				"Only Topaz's own folders are searched besides the program folder: "+string.Join("; ",listFolders));
		}
	}
}
