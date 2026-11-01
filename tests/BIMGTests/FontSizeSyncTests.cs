// 25.3 v25_3-imaging-font-autocalc: in the imaging Edit Text window, typing in Actual Size updates Apparent Size at once and the other way round,
// without the two boxes updating each other in a loop. Licensed under the GNU General Public License, version 2 or later. See LICENSE in the
// repository root.
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenDentBusiness;

namespace BIMGTests {
	[TestClass]
	public class FontSizeSyncTests {
		///<summary>Stands in for the two TextVDouble boxes of the window: setting the text of a box raises its text-changed event, like WPF does,
		///whether the user typed or code set it.</summary>
		private class FakeWindow {
			public string TextActual="";
			public string TextApparent="";
			public int CountSetActual;
			public int CountSetApparent;
			public readonly ImageDrawFontSizeSync Sync;

			public FakeWindow(float zoomVal,float scale=1) {
				Sync=new ImageDrawFontSizeSync(zoomVal,scale,actionSetActualText:SetActual,actionSetApparentText:SetApparent);
			}

			public void SetActual(string text) {
				CountSetActual++;
				if(CountSetActual>100) {
					throw new Exception("The boxes update each other in a loop.");
				}
				TextActual=text;
				Sync.OnActualTextChanged(text);//the text-changed event of the box
			}

			public void SetApparent(string text) {
				CountSetApparent++;
				if(CountSetApparent>100) {
					throw new Exception("The boxes update each other in a loop.");
				}
				TextApparent=text;
				Sync.OnApparentTextChanged(text);
			}

			///<summary>The user types the text into Actual Size one character at a time.</summary>
			public void TypeActual(string text) {
				for(int i=1;i<=text.Length;i++) {
					SetActual(text.Substring(0,i));
				}
			}

			public void TypeApparent(string text) {
				for(int i=1;i<=text.Length;i++) {
					SetApparent(text.Substring(0,i));
				}
			}
		}

		[TestMethod]
		public void TypingActual_UpdatesApparentAtOnce() {
			FakeWindow window=new FakeWindow(zoomVal:50);
			window.SetActual("20");
			Assert.AreEqual("10",window.TextApparent,"At 50% zoom, text appears at half its size.");
			Assert.AreEqual("20",window.TextActual,"The box being typed in is never rewritten.");
			Assert.AreEqual(1,window.CountSetActual,"Only the user's own keystroke set Actual Size.");
			Assert.AreEqual(1,window.CountSetApparent,"Apparent Size was set once, and its event did not set Actual Size back.");
		}

		[TestMethod]
		public void TypingApparent_UpdatesActualAtOnce() {
			FakeWindow window=new FakeWindow(zoomVal:200);
			window.SetApparent("10");
			Assert.AreEqual("5",window.TextActual,"At 200% zoom, text appears at twice its size.");
			Assert.AreEqual("10",window.TextApparent);
			Assert.AreEqual(1,window.CountSetActual);
			Assert.AreEqual(1,window.CountSetApparent);
		}

		[TestMethod]
		public void EveryKeystroke_KeepsTheBoxesInStep() {
			FakeWindow window=new FakeWindow(zoomVal:100);
			window.TypeActual("12.5");
			Assert.AreEqual("12.5",window.TextApparent);
			window.TypeApparent("8");
			Assert.AreEqual("8",window.TextActual);
			Assert.AreEqual("8",window.TextApparent);
		}

		[TestMethod]
		public void InvalidOrBlankText_IsIgnoredUntilValid() {
			FakeWindow window=new FakeWindow(zoomVal:50);
			window.SetActual("24");
			Assert.AreEqual("12",window.TextApparent);
			foreach(string text in new[] { "","  ","abc","1a","-4","0","0.5","200000000","12,,3x" }) {
				window.SetActual(text);
				Assert.AreEqual("12",window.TextApparent,"'"+text+"' leaves Apparent Size alone.");
			}
			window.SetActual("30");
			Assert.AreEqual("15",window.TextApparent,"Valid again: updates again.");
			window.SetApparent("");
			Assert.AreEqual("30",window.TextActual,"Clearing Apparent Size leaves Actual Size alone.");
		}

		[TestMethod]
		public void ScreenScale_IsPartOfTheMath() {
			//At 144 dpi (scale 1.5) and 100% zoom, an actual size of 15 appears as 10, the same math as before 25.3.
			FakeWindow window=new FakeWindow(zoomVal:100,scale:1.5f);
			window.SetActual("15");
			Assert.AreEqual("10",window.TextApparent);
			window.SetApparent("20");
			Assert.AreEqual("30",window.TextActual);
		}

		[TestMethod]
		public void AtMostTwoDecimals() {
			FakeWindow window=new FakeWindow(zoomVal:33);
			window.SetActual("7");
			Assert.AreEqual("2.31",window.TextApparent);
			Assert.AreEqual("7.5",ImageDrawFontSizeSync.FormatSize(7.5f));
			Assert.AreEqual("8",ImageDrawFontSizeSync.FormatSize(8f));
		}

		[TestMethod]
		public void ZeroZoom_ChangesNothing() {
			FakeWindow window=new FakeWindow(zoomVal:0);
			window.SetActual("12");
			Assert.AreEqual("",window.TextApparent,"No division by zero.");
			window.SetApparent("12");
			Assert.AreEqual("12",window.TextActual);
		}

		[TestMethod]
		public void Conversions_RoundTrip() {
			float[] arrayZooms=new[] { 10f,33.3f,50f,100f,150f,400f };
			float[] arraySizes=new[] { 1f,8f,12.5f,72f,1000f };
			foreach(float zoom in arrayZooms) {
				foreach(float size in arraySizes) {
					float apparent=ImageDrawFontSizeSync.ToApparent(size,zoom,1.25f);
					Assert.AreEqual(size,ImageDrawFontSizeSync.ToActual(apparent,zoom,1.25f),size/10000f,"zoom "+zoom+", size "+size);
				}
			}
		}

		[TestMethod]
		public void TryParseSize_SameLimitsAsTheBoxes() {
			float size;
			Assert.IsTrue(ImageDrawFontSizeSync.TryParseSize("1",out size));
			Assert.AreEqual(1f,size);
			Assert.IsTrue(ImageDrawFontSizeSync.TryParseSize("100000000",out size));
			Assert.IsTrue(ImageDrawFontSizeSync.TryParseSize("12.",out size),"A trailing point while typing a decimal is fine.");
			Assert.AreEqual(12f,size);
			Assert.IsFalse(ImageDrawFontSizeSync.TryParseSize("0.99",out size));
			Assert.IsFalse(ImageDrawFontSizeSync.TryParseSize("100000001",out size));
			Assert.IsFalse(ImageDrawFontSizeSync.TryParseSize(null,out size));
			Assert.IsFalse(ImageDrawFontSizeSync.TryParseSize("NaN",out size));
		}

		///<summary>The window builds the sync object on load: until then a text-changed event (from the designer's initial values) must do nothing.
		///A sync object with no actions never throws.</summary>
		[TestMethod]
		public void NoActions_NothingHappens() {
			ImageDrawFontSizeSync sync=new ImageDrawFontSizeSync(100,1,null,null);
			sync.OnActualTextChanged("12");
			sync.OnApparentTextChanged("12");
			List<string> listSet=new List<string>();
			sync=new ImageDrawFontSizeSync(100,1,listSet.Add,listSet.Add);
			sync.OnActualTextChanged("12");
			CollectionAssert.AreEqual(new[] { "12" },listSet);
		}
	}
}
