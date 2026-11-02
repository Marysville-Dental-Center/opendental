// v25_2-ui-blur-module-switch: blur when switching between modules.
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenPmsDental;
using OpenPmsDentalBusiness;
using OpenPmsDentalBusiness.ForkMigrations;

namespace BSHELLTests {
	[TestClass]
	public class ModuleSwitchBlurTests {
		[TestMethod]
		public void ShouldBlur_OnlyForUserClicksWithThePreferenceOn() {
			Assert.IsTrue(ModuleSwitchBlur.ShouldBlur(isEnabled:true,isUserClick:true));
			Assert.IsFalse(ModuleSwitchBlur.ShouldBlur(isEnabled:true,isUserClick:false),"Module changes made by code never blur.");
			Assert.IsFalse(ModuleSwitchBlur.ShouldBlur(isEnabled:false,isUserClick:true));
			Assert.IsFalse(ModuleSwitchBlur.ShouldBlur(isEnabled:false,isUserClick:false));
		}

		[TestMethod]
		public void BlurBitmap_SameSize_SoftensASharpEdge() {
			using Bitmap bitmap=new Bitmap(64,32);
			using(Graphics g=Graphics.FromImage(bitmap)) {
				g.FillRectangle(Brushes.Black,0,0,32,32);
				g.FillRectangle(Brushes.White,32,0,32,32);
			}
			using Bitmap bitmapBlurred=ModuleSwitchBlur.BlurBitmap(bitmap);
			Assert.AreEqual(bitmap.Size,bitmapBlurred.Size);
			//Right at the edge the sharp image jumps from black to white; the blurred one passes through grey.
			int brightnessAtEdge=bitmapBlurred.GetPixel(31,16).R;
			Assert.IsTrue(brightnessAtEdge>20 && brightnessAtEdge<235,"Edge pixel "+brightnessAtEdge);
			Assert.IsNull(ModuleSwitchBlur.BlurBitmap(null));
			using Bitmap bitmapTiny=new Bitmap(3,2);
			using Bitmap bitmapTinyBlurred=ModuleSwitchBlur.BlurBitmap(bitmapTiny);
			Assert.AreEqual(bitmapTiny.Size,bitmapTinyBlurred.Size,"Smaller than the factor still works.");
		}

		[TestMethod]
		public void Overlay_NeverTakesFocus_AndUsesTheFallbackWithoutASnapshot() {
			Rectangle rectangle=new Rectangle(10,20,300,200);
			using(ModuleSwitchBlurOverlay overlay=new ModuleSwitchBlurOverlay(rectangle,null,ModuleSwitchBlur.DURATION_MS)) {//Built, never shown.
				Assert.AreEqual(rectangle,overlay.Bounds);
				Assert.AreEqual(FormBorderStyle.None,overlay.FormBorderStyle);
				Assert.IsFalse(overlay.ShowInTaskbar);
				Assert.AreEqual(ModuleSwitchBlur.FALLBACK_OPACITY,overlay.Opacity,0.001,"No snapshot: a translucent cover.");
				Assert.IsTrue((bool)typeof(ModuleSwitchBlurOverlay).GetProperty("ShowWithoutActivation",BindingFlags.Instance|BindingFlags.NonPublic)
					.GetValue(overlay),"Keyboard input stays with the module.");
			}
			Bitmap bitmapBlurred=new Bitmap(300,200);
			using(ModuleSwitchBlurOverlay overlay=new ModuleSwitchBlurOverlay(rectangle,bitmapBlurred,ModuleSwitchBlur.DURATION_MS)) {
				Assert.AreSame(bitmapBlurred,overlay.BackgroundImage);
				Assert.AreEqual(1.0,overlay.Opacity,0.001);
			}
			Assert.AreEqual(400,ModuleSwitchBlur.DURATION_MS);
		}

		[TestMethod]
		public void MainWindow_BlursAfterAModuleBarClick_Only() {
			string source=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDental.cs");
			string clicked=TestPaths.GetMethodBody(source,"private void moduleBar_ButtonClicked(object sender, OpenPmsDental.ButtonClicked_EventArgs e)");
			int indexSelect=clicked.IndexOf("SetModuleSelected(true);");
			int indexBlur=clicked.IndexOf("ShowModuleSwitchBlur(isUserClick:e.OutlookButton!=null);");
			Assert.IsTrue(indexSelect>0 && indexBlur>indexSelect,"The cover goes up after the new module is selected and filled.");
			Assert.AreEqual(1,TestPaths.Count(source,"ShowModuleSwitchBlur("),"Only module bar clicks blur; Go To and refreshes do not.");
			string shell=TestPaths.ReadRepoFile("OpenPmsDental","Main Modules","FormOpenDentalBShell.cs");
			StringAssert.Contains(shell,"ModuleSwitchBlur.ShouldBlur(ModuleSwitchBlur.IsEnabled(),isUserClick)");
			StringAssert.Contains(shell,"m.Result=(IntPtr)MA_NOACTIVATEANDEAT;");
		}

		[TestMethod]
		public void Preferences_MainWindowMisc_HasTheCheckbox() {
			string designer=TestPaths.ReadRepoFile("OpenPmsDental","User Controls","Preferences","UserControlMainWindowMisc.Designer.cs");
			StringAssert.Contains(designer,"\"Blur when switching between modules to prevent accidental clicking\"");
			StringAssert.Contains(designer,"this.Controls.Add(this.checkModuleSwitchBlur);");
			string source=TestPaths.ReadRepoFile("OpenPmsDental","User Controls","Preferences","UserControlMainWindowMisc.cs");
			StringAssert.Contains(source,"checkModuleSwitchBlur.Checked=ModuleSwitchBlur.IsEnabled();");
			StringAssert.Contains(source,"ModuleSwitchBlur.Save(checkModuleSwitchBlur.Checked)");
		}

		[TestMethod]
		public void PrefName_StaysUnregistered_TheForkRowIsUsed() {
			//RegistryTests keeps the proposed Open Dental name out of PrefName until a public source confirms it.
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),"ModuleSwitchBlur"));
			Assert.IsFalse(Enum.IsDefined(typeof(PrefName),ModuleSwitchBlur.PREF_NAME));
			StringAssert.StartsWith(ModuleSwitchBlur.PREF_NAME,"Fork");
		}

		[TestMethod]
		public void NoMigrationStep_AddsTheRow_SoAdoptedDatabasesStayAsTheyAre() {
			Assert.IsFalse(ForkMigrationRunner.GetSteps().Any(x => x.GetType().Name.Contains("ModuleSwitchBlur")));
			string steps=TestPaths.ReadRepoFile("OpenPmsDentalBusiness","Misc","ForkMigrations","BShellSteps.cs");
			Assert.IsFalse(steps.Contains("ModuleSwitchBlur"));
		}

		[TestMethod]
		public void Pref_ReadAndSave() {
			ScratchDb.RequireDb();//No row yet.
			Assert.IsFalse(ModuleSwitchBlur.IsEnabled(),"A missing row means off.");
			Assert.IsTrue(ModuleSwitchBlur.Save(true),"Inserts the missing row.");
			Assert.IsTrue(ModuleSwitchBlur.IsEnabled());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT ValueString FROM preference WHERE PrefName='"+ModuleSwitchBlur.PREF_NAME+"'"));
			Assert.IsFalse(ModuleSwitchBlur.Save(true),"No change.");
			Assert.IsTrue(ModuleSwitchBlur.Save(false));
			Assert.IsFalse(ModuleSwitchBlur.IsEnabled());
			Assert.AreEqual("1",DataCore.GetScalar("SELECT COUNT(*) FROM preference WHERE PrefName='"+ModuleSwitchBlur.PREF_NAME+"'"));
		}
	}
}
